using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Threading.Tasks;

namespace IRSI.Services.AutoUpdate.Utilities
{
    public static class IO
    {
        public static void CopyFilesRecursively(IDirectoryInfo source, IDirectoryInfo target)
        {
            foreach (var dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (var file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, file.Name));
        }

        public static void CreateFolders(IFileSystem fileSystem, IEnumerable<string> folderNames)
        {
            foreach (var folderName in folderNames)
                if (!fileSystem.Directory.Exists(folderName))
                    fileSystem.Directory.CreateDirectory(folderName);
        }

        public static async Task SaveBytesToFile(IFileSystem fileSystem, string path, byte[] bytes)
        {
            await using var memoryStream = new MemoryStream(bytes);
            await using var fs = fileSystem.FileStream.Create(path, FileMode.Create);
            await memoryStream.CopyToAsync(fs);
        }

        public static async Task ExtractArchiveToPath(IFileSystem fileSystem, string assetFilePath, string targetPath)
        {
            await using var archiveFile = fileSystem.File.OpenRead(assetFilePath);
            var archive = new ZipArchive(archiveFile, ZipArchiveMode.Read, true);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    fileSystem.Directory.CreateDirectory(fileSystem.Path.Combine(targetPath, entry.FullName));
                    continue;
                }

                var destination = Path.GetFullPath(Path.Combine(targetPath, entry.FullName));
                var archiveStream = entry.Open();
                var destinationFile = fileSystem.File.Create(destination);
                await archiveStream.CopyToAsync(destinationFile);

                destinationFile.Close();
                archiveStream.Close();
            }
        }
    }
}