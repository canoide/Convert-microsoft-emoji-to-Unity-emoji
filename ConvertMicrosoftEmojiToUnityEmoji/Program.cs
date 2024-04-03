using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using Svg.Skia;

namespace EmojiTool;

public class Program
{
    public enum DirectoryType
    {
        Flat,
        Color,
        HighContrast,
        ThreeD
    }
    
    public static void Main(string[] args)
    {
        var rootPath = args[0];
        var size = Convert.ToInt32(args[1]);
        Enum.TryParse(args[2], out DirectoryType directoryType);
        
        if (Convert.ToBoolean(args[3]))
        {
            Console.WriteLine("********** Start convert svg. **********");
            ConvertAllSvgToPng(rootPath, size, directoryType);
            Console.WriteLine("********** Convert svg Completed. **********");
        }

        var gridSize = Convert.ToInt32(args[4]);
        var outputPath = args[5];
        var gridImageName = args[6];
        Console.WriteLine("********** Start create grid. **********");
        CreateGridImage(gridImageName, rootPath, outputPath, gridSize, directoryType);
        Console.WriteLine("********** Completed. **********");
    }

    public static void ConvertAllSvgToPng(string rootPath, int size, DirectoryType directoryType)
    {
        // Map DirectoryType to the correct directory name
        string directoryName = directoryType switch
        {
            DirectoryType.ThreeD => "3D",
            DirectoryType.HighContrast => "High Contrast",
            _ => directoryType.ToString()
        };

        // Use different search pattern based on directory type
        string searchPattern = directoryType == DirectoryType.ThreeD ? "*.png" : "*.svg";

        // Find all directories and subdirectories in the root directory.
        string[] directories = Directory.GetDirectories(rootPath, "*" + directoryName + "*", SearchOption.AllDirectories);

        foreach (string dir in directories)
        {
            // Find all SVG or PNG files in the directory.
            string[] files = Directory.GetFiles(dir, searchPattern);

            foreach (string file in files)
            {
                // Define the destination PNG file name based on the SVG or PNG file name.
                string pngFile = Path.Combine(dir, Path.GetFileNameWithoutExtension(file) + ".png");

                // Don't convert 3D images as they are already PNG
                if (directoryType != DirectoryType.ThreeD)
                {
                    ConvertSvgToPng(file, pngFile, size);
                }
            }
        }
    }

    private static void ConvertSvgToPng(string inputPath, string outputPath, int size)
    {
        // Load SVG
        var svg = new SKSvg();
        svg.Load(inputPath);

        // Calculate the scale factors
        float scaleX = size / (float)svg.Picture.CullRect.Width;
        float scaleY = size / (float)svg.Picture.CullRect.Height;

        // Create bitmap
        var bitmap = new SKBitmap(size, size);

        // Create canvas and render SVG
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint()
        {
            IsAntialias = true,  // Enable antialiasing
        };
        canvas.Scale(scaleX, scaleY);
        canvas.DrawPicture(svg.Picture, paint);

        // Save the bitmap as PNG
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }

    public static void CreateGridImage(string imageName, string inputPath, string outputPath, int gridSize,
        DirectoryType directoryType)
    {
        // Map DirectoryType to the correct directory name
        string directoryName = directoryType switch
        {
            DirectoryType.ThreeD => "3D",
            DirectoryType.HighContrast => "High Contrast",
            _ => directoryType.ToString()
        };
    
        string[] directories = Directory.GetDirectories(inputPath, "*", SearchOption.TopDirectoryOnly);
        List<string> pngFiles = new List<string>();
        List<object> metaDataList = new List<object>();
        foreach (string dir in directories)
        {
            string metaFileDir = $"{dir}\\metadata.json";
            if (!File.Exists(metaFileDir))
            {
                throw new Exception($"Directory {dir} does not contain a metadata.json file!");
            }

            string metadata = File.ReadAllText(metaFileDir);
            if (Directory.Exists($"{dir}\\{directoryName}"))
            {
                pngFiles.AddRange(Directory.GetFiles($"{dir}\\{directoryName}", "*.png"));
            }
            else if (Directory.Exists($"{dir}\\Default"))
            {
                pngFiles.AddRange(Directory.GetFiles($"{dir}\\Default\\{directoryName}", "*.png"));
            }
            else
            {
                throw new Exception($"Directory {dir} does not contain any PNG files!");
            }

            metaDataList.Add(JsonConvert.DeserializeObject<JObject>(metadata));
        }

        if (pngFiles.Count == 0) return;

        Image firstImage = Image.FromFile(pngFiles[0]);
        int thumbSize = firstImage.Width;

        int totalImages = pngFiles.Count;
        int perRow = gridSize / thumbSize;
        int curImageIndex = 0;
        int gridCount = 0;
        while (curImageIndex < totalImages)
        {
            List<JObject> currentMetaDataList = new List<JObject>();
            Bitmap gridBitmap = new Bitmap(gridSize, gridSize);
            using (Graphics g = Graphics.FromImage(gridBitmap))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                for (int y = 0, curRow = 0; y < gridSize && curRow < perRow; y += thumbSize, curRow++)
                {
                    for (int x = 0, curCol = 0;
                         x < gridSize && curCol < perRow && curImageIndex < totalImages;
                         x += thumbSize, curCol++)
                    {
                        Image thumbImage = Image.FromFile(pngFiles[curImageIndex]);
                        g.DrawImage(thumbImage, x, y, thumbSize, thumbSize);
                        JObject currentMetaObj = (JObject)metaDataList[curImageIndex];
                        var unicode = currentMetaObj["unicode"].Value<string>().Split(' ')[0];
                        currentMetaObj["filename"] = $"{unicode}.png";
                        currentMetaObj["frame"] =
                            JObject.Parse($"{{\"x\":{x},\"y\":{y},\"w\":{thumbSize},\"h\":{thumbSize}}}");
                        currentMetaObj["rotated"] = false;
                        currentMetaObj["trimmed"] = false;
                        currentMetaObj["spriteSourceSize"] =
                            JObject.Parse($"{{\"x\":0,\"y\":0,\"w\":{thumbSize},\"h\":{thumbSize}}}");
                        currentMetaObj["sourceSize"] = JObject.Parse($"{{\"w\":{thumbSize},\"h\":{thumbSize}}}");
                        currentMetaObj["pivot"] = JObject.Parse("{\"x\":0.5,\"y\":0.5}");
                        currentMetaDataList.Add(currentMetaObj);
                        curImageIndex++;
                    }
                }
            }
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            string gridImagePath = Path.Combine(outputPath, $"{imageName}_{gridCount}.png");
            gridBitmap.Save(gridImagePath, ImageFormat.Png);
            string emojiMetaPath = Path.Combine(outputPath, $"{imageName}_{gridCount}.json");

            JObject finalMetaObj = new JObject();
            finalMetaObj["frames"] = JToken.FromObject(currentMetaDataList);
            finalMetaObj["meta"] = JObject.Parse(
                $"{{\"version\": \"1.0\",\"image\": \"{Path.GetFileName(gridImagePath)}\",\"format\": \"RGBA8888\",\"size\": {{\"w\":{gridSize},\"h\":{gridSize}}},\"scale\": \"1\"}}");
            File.WriteAllText(emojiMetaPath, JsonConvert.SerializeObject(finalMetaObj, Formatting.Indented));
            gridCount++;
        }
    }
}