// Copyright (C) 2015-2024 The Neo Project.
//
// FileNameUtilities.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.BlockchainToolkit
{
    // This Library contains logic that need to handle both Windows and Unix style paths. For example, debug info can be created on Windows and read
    // on Mac/Linux or vis versa. So the standard Path.GetFileName/GetDirectoryName method will not work.

    // This logic lifted from https://github.com/dotnet/symreader-portable/blob/main/src/Microsoft.DiaSymReader.PortablePdb/Utilities/FileNameUtilities.cs
    internal static class FileNameUtilities
    {
        private const char DirectorySeparatorChar = '\\';
        private const char AltDirectorySeparatorChar = '/';
        private const char VolumeSeparatorChar = ':';

        /// <summary>
        /// Returns the position in given path where the file name starts.
        /// </summary>
        /// <returns>-1 if path is null.</returns>
        internal static int IndexOfFileName(string path)
        {
            if (path == null)
            {
                return -1;
            }

            for (int i = path.Length - 1; i >= 0; i--)
            {
                char ch = path[i];
                if (ch == DirectorySeparatorChar || ch == AltDirectorySeparatorChar || ch == VolumeSeparatorChar)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        internal static bool IsDirectorySeparator(char separator)
        {
            return separator == DirectorySeparatorChar || separator == AltDirectorySeparatorChar;
        }

        internal static string GetFileName(string path)
        {
            int fileNameStart = IndexOfFileName(path);
            return (fileNameStart <= 0) ? path : path.Substring(fileNameStart);
        }

        internal static string GetDirectoryName(string path)
        {
            int fileNameStart = IndexOfFileName(path);
            while (fileNameStart >= 0
                && IsDirectorySeparator(path[fileNameStart - 1]))
            {
                fileNameStart--;
            }

            return (fileNameStart <= 0) ? path : path.Substring(0, fileNameStart);
        }

        internal static string TrimStartDirectorySeparators(string path)
        {
            if (path.Length == 0)
                return path;

            var i = 0;
            while (IsDirectorySeparator(path[i]))
            {
                i++;
            }

            return i == 0 ? path : path.Substring(i);
        }
    }
}
