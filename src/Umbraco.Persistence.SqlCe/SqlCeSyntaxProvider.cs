using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.Options;
using NPoco;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseModelDefinitions;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using ColumnInfo = Umbraco.Cms.Infrastructure.Persistence.SqlSyntax.ColumnInfo;
using Constants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Cms.Persistence.SqlCe
{
    /// <summary>
    /// Represents an SqlSyntaxProvider for Sql Ce
    /// </summary>
    public class SqlCeSyntaxProvider : MicrosoftSqlSyntaxProviderBase<SqlCeSyntaxProvider>
    {
        private readonly IOptions<GlobalSettings> _globalSettings;

        public SqlCeSyntaxProvider(IOptions<GlobalSettings> globalSettings)
        {
            _globalSettings = globalSettings;
            BlobColumnDefinition = "IMAGE";
            // NOTE: if this column type is used in sqlce, it will prob result in errors since
            // SQLCE cannot support this type correctly without 2x columns and a lot of work arounds.
            // We don't use this natively within Umbraco but 3rd parties might with SQL server.
            DateTimeOffsetColumnDefinition = "DATETIME"; 
        }

        public override string ProviderName => Constants.DatabaseProviders.SqlCe;

        public override Sql<ISqlContext> SelectTop(Sql<ISqlContext> sql, int top)
        {
            return new Sql<ISqlContext>(sql.SqlContext, sql.SQL.Insert(sql.SQL.IndexOf(' '), " TOP " + top), sql.Arguments);
        }

        public override bool SupportsClustered()
        {
            return false;
        }

        /// <summary>
        /// SqlCe doesn't support the Truncate Table syntax, so we just have to do a DELETE FROM which is slower but we have no choice.
        /// </summary>
        public override string TruncateTable
        {
            get { return "DELETE FROM {0}"; }
        }

        public override string GetIndexType(IndexTypes indexTypes)
        {
            string indexType;
            //NOTE Sql Ce doesn't support clustered indexes
            if (indexTypes == IndexTypes.Clustered)
            {
                indexType = "NONCLUSTERED";
            }
            else
            {
                indexType = indexTypes == IndexTypes.NonClustered
                    ? "NONCLUSTERED"
                    : "UNIQUE NONCLUSTERED";
            }
            return indexType;
        }

        public override string GetConcat(params string[] args)
        {
            return "(" + string.Join("+", args) + ")";
        }

        public override System.Data.IsolationLevel DefaultIsolationLevel => System.Data.IsolationLevel.RepeatableRead;
        public override string DbProvider => "SqlServerCE";

        public override string FormatColumnRename(string tableName, string oldName, string newName)
        {
            //NOTE Sql CE doesn't support renaming a column, so a new column needs to be created, then copy data and finally remove old column
            //This assumes that the new column has been created, and that the old column will be deleted after this statement has run.
            //http://stackoverflow.com/questions/3967353/microsoft-sql-compact-edition-rename-column

            return string.Format("UPDATE {0} SET {1} = {2}", tableName, newName, oldName);
        }

        public override string FormatTableRename(string oldName, string newName)
        {
            return string.Format(RenameTable, oldName, newName);
        }

        public override string FormatPrimaryKey(TableDefinition table)
        {
            var columnDefinition = table.Columns.FirstOrDefault(x => x.IsPrimaryKey);
            if (columnDefinition == null)
                return string.Empty;

            string constraintName = string.IsNullOrEmpty(columnDefinition.PrimaryKeyName)
                                        ? string.Format("PK_{0}", table.Name)
                                        : columnDefinition.PrimaryKeyName;

            string columns = string.IsNullOrEmpty(columnDefinition.PrimaryKeyColumns)
                                 ? GetQuotedColumnName(columnDefinition.Name)
                                 : string.Join(", ", columnDefinition.PrimaryKeyColumns
                                                                     .Split(Constants.CharArrays.CommaSpace, StringSplitOptions.RemoveEmptyEntries)
                                                                     .Select(GetQuotedColumnName));

            return string.Format(CreateConstraint,
                                 GetQuotedTableName(table.Name),
                                 GetQuotedName(constraintName),
                                 "PRIMARY KEY",
                                 columns);
        }

        public override IEnumerable<string> GetTablesInSchema(IDatabase db)
        {
            var items = db.Fetch<dynamic>("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES");
            return items.Select(x => x.TABLE_NAME).Cast<string>().ToList();
        }

        public override IEnumerable<ColumnInfo> GetColumnsInSchema(IDatabase db)
        {
            var items = db.Fetch<dynamic>("SELECT TABLE_NAME, COLUMN_NAME, ORDINAL_POSITION, COLUMN_DEFAULT, IS_NULLABLE, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS");
            return
                items.Select(
                    item =>
                    new ColumnInfo(item.TABLE_NAME, item.COLUMN_NAME, item.ORDINAL_POSITION, item.COLUMN_DEFAULT,
                                   item.IS_NULLABLE, item.DATA_TYPE)).ToList();
        }

        /// <inheritdoc />
        public override IEnumerable<Tuple<string, string>> GetConstraintsPerTable(IDatabase db)
        {
            var items = db.Fetch<dynamic>("SELECT TABLE_NAME, CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS");
            return items.Select(item => new Tuple<string, string>(item.TABLE_NAME, item.CONSTRAINT_NAME)).ToList();
        }

        /// <inheritdoc />
        public override IEnumerable<Tuple<string, string, string>> GetConstraintsPerColumn(IDatabase db)
        {
            var items = db.Fetch<dynamic>("SELECT TABLE_NAME, COLUMN_NAME, CONSTRAINT_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE");
            return items.Select(item => new Tuple<string, string, string>(item.TABLE_NAME, item.COLUMN_NAME, item.CONSTRAINT_NAME)).ToList();
        }

        /// <inheritdoc />
        public override IEnumerable<Tuple<string, string, string, bool>> GetDefinedIndexes(IDatabase db)
        {
            var items =
                db.Fetch<dynamic>(
                    @"SELECT TABLE_NAME, INDEX_NAME, COLUMN_NAME, [UNIQUE] FROM INFORMATION_SCHEMA.INDEXES
WHERE PRIMARY_KEY=0
ORDER BY TABLE_NAME, INDEX_NAME");
            return
                items.Select(
                    item => new Tuple<string, string, string, bool>(item.TABLE_NAME, item.INDEX_NAME, item.COLUMN_NAME, item.UNIQUE));
        }

        /// <inheritdoc />
        public override bool TryGetDefaultConstraint(IDatabase db, string tableName, string columnName, out string constraintName)
        {
            // cannot return a true default constraint name (does not exist on SqlCe)
            // but we won't really need it anyways - just check whether there is a constraint
            constraintName = null;
            var hasDefault = db.Fetch<bool>(@"select column_hasdefault from information_schema.columns
where table_name=@0 and column_name=@1", tableName, columnName).FirstOrDefault();
            return hasDefault;
        }

        public override bool DoesTableExist(IDatabase db, string tableName)
        {
            var result =
                db.ExecuteScalar<long>("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName",
                                       new { TableName = tableName });

            return result > 0;
        }

        public override void WriteLock(IDatabase db, TimeSpan timeout, int lockId)
        {
            // soon as we get Database, a transaction is started

            if (db.Transaction.IsolationLevel < IsolationLevel.RepeatableRead)
                throw new InvalidOperationException("A transaction with minimum RepeatableRead isolation level is required.");

            ObtainWriteLock(db, timeout, lockId);
        }

        public override void WriteLock(IDatabase db, params int[] lockIds)
        {
            // soon as we get Database, a transaction is started

            if (db.Transaction.IsolationLevel < IsolationLevel.RepeatableRead)
                throw new InvalidOperationException("A transaction with minimum RepeatableRead isolation level is required.");

            var timeout = _globalSettings.Value.SqlWriteLockTimeOut;

            foreach (var lockId in lockIds)
            {
                ObtainWriteLock(db, timeout, lockId);
            }
        }

        private static void ObtainWriteLock(IDatabase db, TimeSpan timeout, int lockId)
        {
            db.Execute(@"SET LOCK_TIMEOUT " + timeout.TotalMilliseconds  + ";");
            var i = db.Execute(@"UPDATE umbracoLock SET value = (CASE WHEN (value=1) THEN -1 ELSE 1 END) WHERE id=@id", new { id = lockId });
            if (i == 0) // ensure we are actually locking!
                throw new ArgumentException($"LockObject with id={lockId} does not exist.");
        }

        public override void ReadLock(IDatabase db, TimeSpan timeout, int lockId)
        {
            // soon as we get Database, a transaction is started

            if (db.Transaction.IsolationLevel < IsolationLevel.RepeatableRead)
                throw new InvalidOperationException("A transaction with minimum RepeatableRead isolation level is required.");

            ObtainReadLock(db, timeout, lockId);
        }

        public override void ReadLock(IDatabase db, params int[] lockIds)
        {
            // soon as we get Database, a transaction is started

            if (db.Transaction.IsolationLevel < IsolationLevel.RepeatableRead)
                throw new InvalidOperationException("A transaction with minimum RepeatableRead isolation level is required.");

            foreach (var lockId in lockIds)
            {
                ObtainReadLock(db, null, lockId);
            }
        }

        private static void ObtainReadLock(IDatabase db, TimeSpan? timeout, int lockId)
        {
            if (timeout.HasValue)
            {
                db.Execute(@"SET LOCK_TIMEOUT " + timeout.Value.TotalMilliseconds + ";");
            }

            var i = db.ExecuteScalar<int?>("SELECT value FROM umbracoLock WHERE id=@id", new {id = lockId});

            if (i == null) // ensure we are actually locking!
                throw new ArgumentException($"LockObject with id={lockId} does not exist.");
        }

        protected override string FormatIdentity(ColumnDefinition column)
        {
            return column.IsIdentity ? GetIdentityString(column) : string.Empty;
        }

        private static string GetIdentityString(ColumnDefinition column)
        {
            if (column.Seeding != default(int))
                return string.Format("IDENTITY({0},1)", column.Seeding);

            return "IDENTITY(1,1)";
        }

        protected override string FormatSystemMethods(SystemMethods systemMethod)
        {
            switch (systemMethod)
            {
                case SystemMethods.NewGuid:
                    return "NEWID()";
                case SystemMethods.CurrentDateTime:
                    return "GETDATE()";
                //case SystemMethods.NewSequentialId:
                //    return "NEWSEQUENTIALID()";
                //case SystemMethods.CurrentUTCDateTime:
                //    return "GETUTCDATE()";
            }

            return null;
        }

        public override string DeleteDefaultConstraint
        {
            get
            {
                return "ALTER TABLE {0} ALTER COLUMN {1} DROP DEFAULT";
            }
        }

        public override string DropIndex { get { return "DROP INDEX {1}.{0}"; } }
        public override string CreateIndex => "CREATE {0}{1}INDEX {2} ON {3} ({4})";
        public override string Format(IndexDefinition index)
        {
            var name = string.IsNullOrEmpty(index.Name)
                ? $"IX_{index.TableName}_{index.ColumnName}"
                : index.Name;

            var columns = index.Columns.Any()
                ? string.Join(",", index.Columns.Select(x => GetQuotedColumnName(x.Name)))
                : GetQuotedColumnName(index.ColumnName);


            return string.Format(CreateIndex, GetIndexType(index.IndexType), " ", GetQuotedName(name),
                                 GetQuotedTableName(index.TableName), columns);
        }

        public override string GetSpecialDbType(SpecialDbType dbTypes)
        {
            // SqlCE does not have nvarchar(max) for now
            if (dbTypes == SpecialDbType.NVARCHARMAX)
            {
                return "NTEXT";
            }

            return base.GetSpecialDbType(dbTypes);
        }
        public override SqlDbType GetSqlDbType(DbType dbType)
        {
            if (DbType.Binary == dbType)
            {
                return SqlDbType.Image;
            }
            return base.GetSqlDbType(dbType);
        }
    }
}
