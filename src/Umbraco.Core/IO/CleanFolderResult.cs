﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Umbraco.Cms.Core.IO
{
    public class CleanFolderResult
    {
        private CleanFolderResult()
        {
        }

        public CleanFolderResultStatus Status { get; private set; }

        public IReadOnlyCollection<Error> Errors { get; private set; }

        public static CleanFolderResult Success()
        {
            return new CleanFolderResult { Status = CleanFolderResultStatus.Success };
        }

        public static CleanFolderResult FailedAsDoesNotExist()
        {
            return new CleanFolderResult { Status = CleanFolderResultStatus.FailedAsDoesNotExist };
        }

        public static CleanFolderResult FailedWithErrors(List<Error> errors)
        {
            return new CleanFolderResult
            {
                Status = CleanFolderResultStatus.FailedWithException,
                Errors = errors.AsReadOnly(),
            };
        }

        public class Error
        {
            public Error(Exception exception, FileInfo erroringFile)
            {
                Exception = exception;
                ErroringFile = erroringFile;
            }

            public Exception Exception { get; set; }

            public FileInfo ErroringFile { get; set; }
        }
    }
}
