using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AnimalTrainer.Model
{
    public sealed class ImageRow
    {
        public string ImagePath { get; set; } = "";
        public string Label { get; set; } = "";

        public static IEnumerable<ImageRow> ReadFromFolders(string root)
        {
            if (!Directory.Exists(root)) yield break;

            foreach (var classDir in Directory.GetDirectories(root))
            {
                var label = Path.GetFileName(classDir);

                foreach (var img in Directory.EnumerateFiles(classDir, "*.*", SearchOption.AllDirectories)
                                             .Where(HasImageExt))
                {
                    var rel = Path.GetRelativePath(root, img).Replace('\\', '/');
                    yield return new ImageRow { ImagePath = rel, Label = label };
                }
            }
        }

        public static string[] GetLabels(string root)
            => Directory.Exists(root)
               ? Directory.GetDirectories(root)
                          .Select(d => Path.GetFileName(d))
                          .OrderBy(s => s)
                          .ToArray()
               : System.Array.Empty<string>();

        private static bool HasImageExt(string p)
        {
            var ext = Path.GetExtension(p).ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".bmp";
        }
    }
}
