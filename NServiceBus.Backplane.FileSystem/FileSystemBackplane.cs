using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NServiceBus.Backplane.FileSystem
{
    public class FileSystemBackplane : BackplaneDefinition
    {
        public override IDataBackplane CreateBackplane(string nodeId, string connectionString)
        {
            var folder = connectionString == null 
                ? CreateUniqueFOlderBasedOnSolutionName() 
                : UseFolderFromConnectionString(connectionString);

            return new FileSystemDataBackplane(nodeId, folder);
        }

        private string UseFolderFromConnectionString(string connectionString)
        {
            if (!Directory.Exists(connectionString))
            {
                throw new Exception("In file-based backplane connection string has to be a path to an existing directory.");
            }
            return connectionString;
        }

        private string CreateUniqueFOlderBasedOnSolutionName()
        {
            string folder;
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            var solutionFile = FindSolutionFile(new DirectoryInfo(currentPath));
            if (solutionFile == null)
            {
                throw new Exception(
                    "Could not find solution (.sln) file on path between the bin folder and drive root. ");
            }
            var tempDir = Path.Combine(Path.GetTempPath(), "NServiceBus.Backplane",
                Path.GetFileNameWithoutExtension(solutionFile));
            Directory.CreateDirectory(tempDir);
            folder = tempDir;
            return folder;
        }

        private string FindSolutionFile(DirectoryInfo currentPath)
        {
            if (currentPath.Parent == null)
            {
                return null;
            }
            var solutionFile = currentPath.EnumerateFiles("*.sln").FirstOrDefault();
            if (solutionFile != null)
            {
                return solutionFile.FullName;
            }
            return FindSolutionFile(currentPath.Parent);
        }
    }
}
