using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spludlow.MameAO
{
    public class Snap
    {
		private static ImageCodecInfo JpegCodecInfo;

		static Snap()
		{
			ImageCodecInfo[] imageDecoders = ImageCodecInfo.GetImageDecoders();
			foreach (ImageCodecInfo imageCodecInfo in imageDecoders)
			{
				if (imageCodecInfo.FormatID == ImageFormat.Jpeg.Guid)
				{
					JpegCodecInfo = imageCodecInfo;
				}
			}

			if (JpegCodecInfo == null)
			{
				throw new ApplicationException("Bitmaps; Can not find Jpeg Encoder");
			}
		}
		public static void ImportSnap(string sourceDirectory, string targetDirectory)
		{
			Size size = new Size(128, 128);

			string targetDirectoryPNG = Path.Combine(targetDirectory, "png");
			Directory.CreateDirectory(targetDirectoryPNG);
			string targetDirectoryJPG = Path.Combine(targetDirectory, "jpg");
			Directory.CreateDirectory(targetDirectoryJPG);

			Console.Write("Clearing Attributes...");
			Tools.ClearAttributes(sourceDirectory);
			Console.WriteLine("...done");

			int processCount;
			int skipCount;
			DateTime startTime;
			string[] sourceFilenames;
			int count;

			//
			// Machine (snap)
			//
			processCount = 0;
			skipCount = 0;
			startTime = DateTime.Now;

			count = 0;
			sourceFilenames = Directory.GetFiles(Path.Combine(sourceDirectory, "snap"), "*.png");
			foreach (string sourceFilename in sourceFilenames)
			{
				if (ImportSnapFile(sourceFilename, targetDirectoryPNG, targetDirectoryJPG, size) == true)
					++processCount;
				else
					++skipCount;

				if ((count++ % 4096) == 0)
					Console.WriteLine($"machine: {count}/{sourceFilenames.Length}");
			}
			Console.WriteLine($"Machine process:{processCount}, skip: {skipCount}, took:{(DateTime.Now - startTime).TotalMinutes}");

			//
			// Software (software list name)
			//
			processCount = 0;
			skipCount = 0;
			startTime = DateTime.Now;

			foreach (string softwareDirectory in Directory.GetDirectories(sourceDirectory))
			{
				string softwarelist_name = Path.GetFileName(softwareDirectory);

				if (softwarelist_name == "snap")
					continue;

				count = 0;
				sourceFilenames = Directory.GetFiles(softwareDirectory, "*.png");

				if (sourceFilenames.Length == 0)
					continue;

				string targetDirectorySoftwarePNG = Path.Combine(targetDirectoryPNG, softwarelist_name);
				Directory.CreateDirectory(targetDirectorySoftwarePNG);

				string targetDirectorySoftwareJPG = Path.Combine(targetDirectoryJPG, softwarelist_name);
				Directory.CreateDirectory(targetDirectorySoftwareJPG);

				foreach (string sourceFilename in sourceFilenames)
				{
					if (ImportSnapFile(sourceFilename, targetDirectorySoftwarePNG, targetDirectorySoftwareJPG, size) == true)
						++processCount;
					else
						++skipCount;

					if ((count++ % 4096) == 0)
						Console.WriteLine($"{softwarelist_name}: {count}/{sourceFilenames.Length}");
				}
			}
			Console.WriteLine($"Software process:{processCount}, skip: {skipCount}, took:{(DateTime.Now - startTime).TotalMinutes}");

			//
			// Index
			//
			IndexSnapDirectory(targetDirectoryPNG);

		}
		public static bool ImportSnapFile(string sourceFilename, string targetDirectoryPNG, string targetDirectoryJPG, Size size)
		{
			FileInfo sourceInfo = new FileInfo(sourceFilename);
			FileInfo targetInfo = null;

			string targetFilename = Path.Combine(targetDirectoryPNG, Path.GetFileName(sourceFilename));

			if (File.Exists(targetFilename) == true)
				targetInfo = new FileInfo(targetFilename);

			if (targetInfo != null && targetInfo.LastWriteTime <= sourceInfo.LastWriteTime)
				return false;

			// PNG
			File.Delete(targetFilename);
			File.Copy(sourceFilename, targetFilename);

			// JPG
			targetFilename = Path.Combine(targetDirectoryJPG, Path.GetFileNameWithoutExtension(sourceFilename) + ".jpg");

			File.Delete(targetFilename);
			Resize(sourceFilename, size, targetFilename, ImageFormat.Jpeg, PixelFormat.Format24bppRgb, 72);
			File.SetLastWriteTime(targetFilename, sourceInfo.LastWriteTime);

			return true;
		}

		public static void IndexSnapDirectory(string directory)
		{
			string indexFilename = Path.Combine(directory, "_index.txt");

			DataTable table = Tools.MakeDataTable("snap", 
				"Key		Length	LastWriteTime	Width	Height	HorizontalResolution	VerticalResolution	PaletteLength	PixelFormat	PropertyItems",
				"String*	Int64	DateTime		Int32	Int32	Single					Single				Int64			String		String"
			);

			int count = 0;
			string[] filenames = Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories);
			foreach (string filename in filenames)
			{
				FileInfo info = new FileInfo(filename);

				string name = info.FullName.Substring(directory.Length + 1);
				name = name.Substring(0, name.Length - 4);


				using (Image image = Image.FromFile(filename))
				{
					string propertyItems = String.Join(", ", image.PropertyItems.Select((PropertyItem item) => {
						string itemResult = $"{item.Id}({item.Type}/{item.Len})";
						if (item.Type == 2)
							itemResult += ":" + new string(item.Value.Select(b => (b >= 32 && b <= 126) ? (char)b : ' ').ToArray()).Trim();
						return itemResult;
					}));

					table.Rows.Add(name, info.Length, info.LastWriteTime,
						image.Width, image.Height, image.HorizontalResolution, image.VerticalResolution,
						image.Palette.Entries.LongLength, image.PixelFormat.ToString(), propertyItems);
				}

				if ((count++ % 4096) == 0)
					Console.WriteLine($"{count}/{filenames.Length}");
			}

			File.Delete(indexFilename);
			File.WriteAllText(indexFilename, Tools.TextTable(table), Encoding.UTF8);

			//Tools.PopText(table);
		}

		public static Size Resize(string sourceFilename, Size size, string targetFilename, ImageFormat imageFormat, PixelFormat pixelFormat, int dpi)
		{
			using (var image = Image.FromFile(sourceFilename))
			{
				return Resize(image, size, targetFilename, imageFormat, pixelFormat, dpi);
			}
		}

		public static Size Resize(Image image, Size size, string filename, ImageFormat imageFormat, PixelFormat pixelFormat, int dpi)
		{
			using (var bitmap = Resize(image, size, pixelFormat, dpi))
			{
				if (imageFormat == ImageFormat.Jpeg)
					QualitySaveJpeg(bitmap, filename, 100);
				else
					bitmap.Save(filename, imageFormat);

				return new Size(bitmap.Width, bitmap.Height);
			}
		}

		public static void QualitySaveJpeg(Bitmap bitmap, string filename, int qualityPercent)
		{
			EncoderParameters encoderParameters = new EncoderParameters(1);
			encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, qualityPercent);
			bitmap.Save(filename, JpegCodecInfo, encoderParameters);
		}

		public static Bitmap Resize(Image image, Size size, PixelFormat pixelFormat, int dpi)
		{
			float num = 0f;
			float num2 = 0f;
			if (size.Width != 0 && size.Height != 0)
			{
				float num3 = (float)image.Width / (float)size.Width;
				float num4 = (float)image.Height / (float)size.Height;
				if (num3 / num4 < 1f)
				{
					size.Width = 0;
				}
				else
				{
					size.Height = 0;
				}
			}

			if (size.Width != 0 && size.Height == 0)
			{
				num = size.Width;
				num2 = (float)image.Height * (num / (float)image.Width);
			}

			if (size.Width == 0 && size.Height != 0)
			{
				num2 = size.Height;
				num = (float)image.Width * (num2 / (float)image.Height);
			}

			if (size.Width == 0 && size.Height == 0)
			{
				num = image.Width;
				num2 = image.Height;
			}

			int num5 = (int)num;
			int num6 = (int)num2;
			if (num5 <= 0 || num6 <= 0)
			{
				throw new ApplicationException("ImageSize.Resize, Bad new Size:\t" + num5 + " x " + num6);
			}

			Bitmap bitmap = new Bitmap(num5, num6, pixelFormat);
			bitmap.SetResolution(dpi, dpi);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				ConfigureGraphicsPixel(graphics);
				graphics.DrawImage(image, new Rectangle(0, 0, num5, num6));
				return bitmap;
			}
		}

		public static void ConfigureGraphicsPixel(Graphics graphics)
		{
			graphics.PageUnit = GraphicsUnit.Pixel;
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		}
	}
}
