// Copyright (c) Umbraco.
// See LICENSE for more details.

using Umbraco.Cms.Core.Trees;
using Umbraco.Cms.Tests.Common.Builders.Interfaces;
using Constants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Cms.Tests.Common.Builders
{
    public class TreeBuilder
        : BuilderBase<Tree>,
            IWithAliasBuilder,
            IWithSortOrderBuilder
    {
        private int? _sortOrder;
        private string _alias;
        private string _sectionAlias;
        private string _group;
        private string _title;
        private TreeUse? _treeUse;
        private bool? _isSingleNode;

        public TreeBuilder WithSectionAlias(string sectionAlias)
        {
            _sectionAlias = sectionAlias;
            return this;
        }

        public TreeBuilder WithGroup(string group)
        {
            _group = group;
            return this;
        }

        public TreeBuilder WithTitle(string title)
        {
            _title = title;
            return this;
        }

        public TreeBuilder WithTreeUse(TreeUse treeUse)
        {
            _treeUse = treeUse;
            return this;
        }

        public TreeBuilder WithTreeUse(bool isSingleNode)
        {
            _isSingleNode = isSingleNode;
            return this;
        }

        public override Tree Build()
        {
            var sortOrder = _sortOrder ?? 1;
            var alias = _alias ?? "testTree";
            var sectionAlias = _sectionAlias ?? Constants.Applications.Content;
            var group = _group ?? string.Empty;
            var title = _title ?? string.Empty;
            TreeUse treeUse = _treeUse ?? TreeUse.Main;
            var isSingleNode = _isSingleNode ?? false;

            return new Tree(sortOrder, sectionAlias, group, alias, title, treeUse, typeof(SampleTreeController), isSingleNode);
        }

        string IWithAliasBuilder.Alias
        {
            get => _alias;
            set => _alias = value;
        }

        int? IWithSortOrderBuilder.SortOrder
        {
            get => _sortOrder;
            set => _sortOrder = value;
        }

        private class SampleTreeController
        {
        }
    }
}
