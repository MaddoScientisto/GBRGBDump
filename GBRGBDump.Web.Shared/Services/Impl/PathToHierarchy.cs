using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.Web.Shared.Services.Impl
{
    public enum ItemType
    {
        Folder,
        File,
        Image,
        Sav,
        Rom
    }

    public class Item
    {
        public string Text { get; set; }
        public IEnumerable<Item> Children { get; set; }
        public ItemType Type { get; set; }

        public string Path { get; set; }
    }

    public class PathToHierarchy
    {
        public Item BuildHierarchy(List<string> paths)
        {
            // Use Path.GetDirectoryName to find the topmost common folder
            var rootFolder = GetTopmostFolder(paths);

            var root = new Item
            {
                Text = rootFolder,
                Children = new List<Item>(),
                Type = ItemType.Folder,
                Path = rootFolder
            };

            foreach (var path in paths)
            {
                // Get the relative path from the root folder to the current path
                var relativePath = Path.GetRelativePath(rootFolder, path);
                AddPath(root, path, relativePath.Split(Path.DirectorySeparatorChar), 0);
            }

            return root;
        }

        private string GetTopmostFolder(List<string> paths)
        {
            // Get the longest common root (i.e., the topmost folder that is common for all paths)
            var commonRoot = Path.GetDirectoryName(paths.First());

            foreach (var path in paths)
            {
                commonRoot = GetCommonPath(commonRoot, Path.GetDirectoryName(path));
            }

            return commonRoot;
        }

        private string GetCommonPath(string path1, string path2)
        {
            var separator = Path.DirectorySeparatorChar;
            var path1Parts = path1.Split(separator);
            var path2Parts = path2.Split(separator);
            int commonLength = Math.Min(path1Parts.Length, path2Parts.Length);

            var commonPathParts = new List<string>();
            for (int i = 0; i < commonLength; i++)
            {
                if (path1Parts[i] == path2Parts[i])
                {
                    commonPathParts.Add(path1Parts[i]);
                }
                else
                {
                    break;
                }
            }

            return string.Join(separator, commonPathParts);
        }

        private void AddPath(Item current, string fullPath, string[] pathParts, int index)
        {
            if (index == pathParts.Length)
            {
                return;
            }

            string currentPart = pathParts[index];
            bool isLastPart = index == pathParts.Length - 1;
            bool isFile = isLastPart && Path.HasExtension(currentPart);  // Check if it is a file by detecting an extension

            ItemType type;
            if (isFile)
            {
                var ext = Path.GetExtension(currentPart);

                type = ext switch
                {
                    ".png" => ItemType.Image,
                    ".jpg" => ItemType.Image,
                    ".gbc" => ItemType.Rom,
                    ".sav" => ItemType.Sav,
                    _ => ItemType.File
                };
            }
            else
                type = ItemType.Folder;

            // Try to find if this part already exists in the current node's children
            var existingChild = current.Children.FirstOrDefault(c => c.Text == currentPart);

            if (existingChild == null)
            {
                // Create a new child item
                existingChild = new Item
                {
                    Text = currentPart,
                    Children = isFile ? null : new List<Item>(),
                    Type = type,
                    Path = fullPath,
                };

                // Add the new item to the current node's children
                ((List<Item>)current.Children).Add(existingChild);
            }

            // If it's a folder, continue traversing down the hierarchy
            if (type == ItemType.Folder)
            {
                AddPath(existingChild, fullPath, pathParts, index + 1);
            }
        }
    }
}
