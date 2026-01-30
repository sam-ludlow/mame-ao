using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Spludlow.MameAO
{
    public class Snap
    {
		public static Size ThumbSize = new Size(128, 128);

		private static ImageCodecInfo JpegCodecInfo = null;

		static Snap()
		{
			ImageCodecInfo[] imageDecoders = ImageCodecInfo.GetImageDecoders()
				.Where(imageCodecInfo => imageCodecInfo.FormatID == ImageFormat.Jpeg.Guid).ToArray();

			if (imageDecoders.Length > 0)
				JpegCodecInfo = imageDecoders[0];
		}

		public static string CollectSnaps(string machine_name)
		{
			string snapFilename = null;

			string snapDirectory = Path.Combine(Globals.Core.Directory, "snap", machine_name);
			if (Directory.Exists(snapDirectory) == true)
			{
				var sourceSnapFilenames = new DirectoryInfo(snapDirectory).GetFiles("*.png").OrderByDescending(fileInfo => fileInfo.LastWriteTime).Select(fileInfo => fileInfo.FullName).ToArray();
				if (sourceSnapFilenames.Length > 0)
				{
					string snapTargetDirectory = Path.Combine(Globals.SnapDirectory, Globals.Core.Name, machine_name);
					Directory.CreateDirectory(snapTargetDirectory);

					string[] snapFilenames = Mame.CollectSnaps(sourceSnapFilenames, snapTargetDirectory, machine_name, Globals.Core.Version, null);
					snapFilename = snapFilenames[0];
				}
			}

			return snapFilename;
		}

		public static DataTable LoadSnapIndex(string snapDirectory, string coreName)
		{
			DataTable table = null;
			string snapIndexFilename = Path.Combine(snapDirectory, coreName, "png", "_index.txt");
			if (File.Exists(snapIndexFilename) == true)
			{
				table = Tools.TextTableReadFile(snapIndexFilename);
				table.TableName = "snap";
				Console.WriteLine($"Snaps loaded: {snapIndexFilename}");
			}
			else
			{
				Console.WriteLine($"Snaps NOT FOUND: {snapIndexFilename}");
			}
			return table;
		}

		public static void ImportSnapMachine(string sourceDirectory, string targetDirectory)
		{
			Size size = new Size(128, 128);

			string targetDirectoryPNG = Path.Combine(targetDirectory, "png");
			Directory.CreateDirectory(targetDirectoryPNG);
			string targetDirectoryJPG = Path.Combine(targetDirectory, "jpg");
			Directory.CreateDirectory(targetDirectoryJPG);

			Console.Write("Clearing Attributes...");
			Tools.ClearAttributes(sourceDirectory);
			Console.WriteLine("...done");

			int processCount = 0;
			int skipCount = 0;
			int count = 0;
			DateTime startTime = DateTime.Now;

			string[] sourceFilenames = Directory.GetFiles(sourceDirectory, "*.png");
			foreach (string sourceFilename in sourceFilenames)
			{
				if (ImportSnapFile(sourceFilename, targetDirectoryPNG, targetDirectoryJPG, size) == true)
					++processCount;
				else
					++skipCount;

				if (sourceFilenames.Length < 1024)
					Console.WriteLine($"machine: {count}/{sourceFilename}");
				else
					if ((count % 4096) == 0)
						Console.WriteLine($"machine: {count}/{sourceFilenames.Length}");
				++count;
			}
			Console.WriteLine($"Machine process:{processCount}, skip: {skipCount}, took:{(DateTime.Now - startTime).TotalMinutes}");
		}

		public static void ImportSnapSoftware(string sourceDirectory, string targetDirectory)
		{
			string targetDirectoryPNG = Path.Combine(targetDirectory, "png");
			Directory.CreateDirectory(targetDirectoryPNG);
			string targetDirectoryJPG = Path.Combine(targetDirectory, "jpg");
			Directory.CreateDirectory(targetDirectoryJPG);

			Console.Write("Clearing Attributes...");
			Tools.ClearAttributes(sourceDirectory);
			Console.WriteLine("...done");

			int processCount = 0;
			int skipCount = 0;
			int count = 0;
			DateTime startTime = DateTime.Now;

			foreach (string softwareDirectory in Directory.GetDirectories(sourceDirectory))
			{
				string softwarelist_name = Path.GetFileName(softwareDirectory);

				if (softwarelist_name == "snap")
					continue;

				count = 0;
				string[] sourceFilenames = Directory.GetFiles(softwareDirectory, "*.png");

				if (sourceFilenames.Length == 0)
					continue;

				string targetDirectorySoftwarePNG = Path.Combine(targetDirectoryPNG, softwarelist_name);
				Directory.CreateDirectory(targetDirectorySoftwarePNG);

				string targetDirectorySoftwareJPG = Path.Combine(targetDirectoryJPG, softwarelist_name);
				Directory.CreateDirectory(targetDirectorySoftwareJPG);

				foreach (string sourceFilename in sourceFilenames)
				{
					if (ImportSnapFile(sourceFilename, targetDirectorySoftwarePNG, targetDirectorySoftwareJPG, ThumbSize) == true)
						++processCount;
					else
						++skipCount;
					if (sourceFilenames.Length < 1024)
						Console.WriteLine($"software: {count}/{sourceFilename}");
					else
						if ((count % 4096) == 0)
							Console.WriteLine($"{softwarelist_name}: {count}/{sourceFilenames.Length}");
				}
			}
			Console.WriteLine($"Software process:{processCount}, skip: {skipCount}, took:{(DateTime.Now - startTime).TotalMinutes}");
		}

		public static bool ImportSnapFile(string sourceFilename, string targetDirectoryPNG, string targetDirectoryJPG, Size size)
		{
			FileInfo sourceInfo = new FileInfo(sourceFilename);
			FileInfo targetInfo = null;

			string targetFilename = Path.Combine(targetDirectoryPNG, Path.GetFileName(sourceFilename));

			if (File.Exists(targetFilename) == true)
				targetInfo = new FileInfo(targetFilename);

			if (targetInfo != null && targetInfo.LastWriteTime >= sourceInfo.LastWriteTime)
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

			DataTable existingTable = table.Clone();
			if (File.Exists(indexFilename) == true)
				existingTable = Tools.TextTableReadFile(indexFilename);

			int count = 0;
			string[] filenames = Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories);
			foreach (string filename in filenames)
			{
				FileInfo info = new FileInfo(filename);

				DateTime lastWriteTime = info.LastWriteTime;
				lastWriteTime = new DateTime(lastWriteTime.Ticks - (lastWriteTime.Ticks % TimeSpan.TicksPerSecond), lastWriteTime.Kind);

				string name = info.FullName.Substring(directory.Length + 1);
				name = name.Substring(0, name.Length - 4);

				DataRow existingRow = existingTable.Rows.Find(name);

				if (existingRow == null || (DateTime)existingRow["LastWriteTime"] != lastWriteTime)
				{
					using (Image image = Image.FromFile(filename))
					{
						string propertyItems = String.Join(", ",
								image.PropertyItems.Where(item => item.Type == 2)
								.Select(item => item.Id + ":" + new string(item.Value.Select(b => (b >= 32 && b <= 126) ? (char)b : ' ').ToArray()).Trim())
							);

						table.Rows.Add(name, info.Length, info.LastWriteTime,
							image.Width, image.Height, image.HorizontalResolution, image.VerticalResolution,
							image.Palette.Entries.LongLength, image.PixelFormat.ToString(), propertyItems);
					}
				}
				else
				{
					table.ImportRow(existingRow);
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
			if (JpegCodecInfo != null)
			{
				EncoderParameters encoderParameters = new EncoderParameters(1);
				encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, qualityPercent);
				bitmap.Save(filename, JpegCodecInfo, encoderParameters);
			}
			else
			{
				bitmap.Save(filename, ImageFormat.Jpeg);
			}
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

		public static void UtilMakeThumbs()
		{
			string directory = @"";

			foreach (string pngFilename in Directory.GetFiles(directory, "*.png"))
			{
				string jpgFilename = Path.Combine(Path.GetDirectoryName(pngFilename), Path.GetFileNameWithoutExtension(pngFilename) + ".jpg");

				if (File.Exists(jpgFilename) == false)
					Snap.Resize(pngFilename, Snap.ThumbSize, jpgFilename, ImageFormat.Jpeg, PixelFormat.Format24bppRgb, 72);
			}
		}

		public static void UtilCopyDatabaseRows(string sourceConnectionString, string targetConnectionString, string sourceTableName, string targetTableName)
		{
			SqlConnection sourceConnection = new SqlConnection(sourceConnectionString);
			SqlConnection targetConnection = new SqlConnection(targetConnectionString);


			Database.ExecuteNonQuery(targetConnection, $"SET IDENTITY_INSERT [{targetTableName}] ON");

			try
			{
				sourceConnection.Open();
				targetConnection.Open();
				try
				{
					using (SqlCommand command = new SqlCommand($"SELECT * FROM [{sourceTableName}]", sourceConnection))
					{
						using (SqlDataReader reader = command.ExecuteReader())
						{
							using (SqlBulkCopy bulkCopy = new SqlBulkCopy(targetConnection, SqlBulkCopyOptions.KeepIdentity, null))
							{
								bulkCopy.DestinationTableName = targetTableName;
								bulkCopy.BatchSize = 4096;
								bulkCopy.BulkCopyTimeout = 0;

								bulkCopy.WriteToServer(reader);
							}
						}
					}
				}
				finally
				{
					sourceConnection.Close();
					targetConnection.Close();
				}
			}
			finally
			{
				Database.ExecuteNonQuery(targetConnection, $"SET IDENTITY_INSERT [{targetTableName}] OFF");
			}
		}

		public static void UtilDeleteDeviceThumbs(string connectionString, string snapCoreDirectory)
		{
			SqlConnection connection = new SqlConnection(connectionString);

			DataTable table = Database.ExecuteFill(connection, "SELECT [name] FROM [machine] WHERE ([isdevice] = 'yes') ORDER BY [name];");

			foreach (DataRow row in table.Rows)
			{
				string machine_name = (string)row["name"];

				string filename;

				filename = Path.Combine(snapCoreDirectory, "png", machine_name + ".png");

				if (File.Exists(filename) == true)
				{
					File.Delete(filename);
					Console.WriteLine(filename);
				}

				filename = Path.Combine(snapCoreDirectory, "jpg", machine_name + ".jpg");
				if (File.Exists(filename) == true)
				{
					File.Delete(filename);
					Console.WriteLine(filename);
				}
			}
		}


	}
}
