
using AForge.Imaging;
using AForge.Imaging.Filters;
using AIMLbot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace AIMLTGBot
{
    internal class Settings
    {
        private int _border = 20;
        public int border
        {
            get
            {
                return _border;
            }
            set
            {
                if ((value > 0) && (value < height / 3))
                {
                    _border = value;
                    if (top > 2 * _border) top = 2 * _border;
                    if (left > 2 * _border) left = 2 * _border;
                }
            }
        }

        public int width = 640;
        public int height = 640;

        /// <summary>
        /// Размер сетки для сенсоров по горизонтали
        /// </summary>
        public int blocksCount = 10;

        /// <summary>
        /// Желаемый размер изображения до обработки
        /// </summary>
        public Size orignalDesiredSize = new Size(500, 500);
        /// <summary>
        /// Желаемый размер изображения после обработки
        /// </summary>
        public Size processedDesiredSize = new Size(500, 500);

        public int margin = 10;
        public int top = 40;
        public int left = 40;

        /// <summary>
        /// Второй этап обработки
        /// </summary>
        public bool processImg = false;

        /// <summary>
        /// Порог при отсечении по цвету 
        /// </summary>
        public byte threshold = 120;
        public float differenceLim = 0.15f;

        public void incTop() { if (top < 2 * _border) ++top; }
        public void decTop() { if (top > 0) --top; }
        public void incLeft() { if (left < 2 * _border) ++left; }
        public void decLeft() { if (left > 0) --left; }



	}

	internal class MagicEye
    {
        /// <summary>
        /// Обработанное изображение
        /// </summary>
        public Bitmap processed;

        /// <summary>
        /// Класс настроек
        /// </summary>
        public Settings settings = new Settings();

        private StudentNetwork network;

        public FigureType result;

		public MagicEye(StudentNetwork network)
        {
            this.network = network;
        }

		public static Sample ConvertToSample(Bitmap bitmap, int classesCount)
		{

			if (bitmap.Width != 100 || bitmap.Height != 100)
			{
				throw new ArgumentException($"Изображение должно быть 100x100 пикселей, а не {bitmap.Width}x{bitmap.Height}");
			}
			double[][] image = new double[100][];

			for (int y = 0; y < 100; y++)
			{
				image[y] = new double[100];
				for (int x = 0; x < 100; x++)
				{
					var pixel = bitmap.GetPixel(x, y);
					double value = (pixel.R + pixel.G + pixel.B) / (3.0 * 255.0);

					// Инвертируем чтобы белый был 0.0, черный - 1.0
					if (value > 0.4)
						value = 0;
					else
						value = 1.0;
					image[y][x] = value;
				}
			}


			double[][,] kernels = new double[][,]
			{
				new double[,] {
					{ -1, -1, -1 },
					{ 0, 0, 0 },
					{ 1, 1, 1 }
				},

				new double[,] {
					{ -1, 0, 1 },
					{ -1, 0, 1 },
					{ -1, 0, 1 }
				},

				new double[,] {
					{ -1, -1, 0 },
					{ -1, 0, 1 },
					{ 0, 1, 1 }
				}
			};

			int outputSize = 98;
			int poolFactor = 7;
			int pooledSize = outputSize / poolFactor;

			double[] inputValues = new double[3 * pooledSize * pooledSize];

			for (int k = 0; k < kernels.Length; k++)
			{
				double[,] kernel = kernels[k];

				for (int py = 0; py < pooledSize; py++)
				{
					for (int px = 0; px < pooledSize; px++)
					{
						double sum = 0;

						for (int dy = 0; dy < poolFactor; dy++)
						{
							for (int dx = 0; dx < poolFactor; dx++)
							{
								int origY = py * poolFactor + dy;
								int origX = px * poolFactor + dx;

								double convValue = 0;
								for (int ky = 0; ky < 3; ky++)
								{
									for (int kx = 0; kx < 3; kx++)
									{
										convValue += image[origY + ky][origX + kx] * kernel[ky, kx];
									}
								}

								sum += System.Math.Abs(convValue);
							}
						}

						sum /= (poolFactor * poolFactor);

						int index = k * (pooledSize * pooledSize) + py * pooledSize + px;
						inputValues[index] = sum;
					}
				}
			}

			return new Sample(inputValues, classesCount);
		}

		public bool ProcessImage(Bitmap bitmap)
        {
			var grayFilter = new Grayscale(0.2125, 0.7154, 0.0721);
			var uProcessed = grayFilter.Apply(UnmanagedImage.FromManagedImage(bitmap));

			var scaleFilter = new ResizeBilinear(
				settings.orignalDesiredSize.Width,
				settings.orignalDesiredSize.Height
			);
			uProcessed = scaleFilter.Apply(uProcessed);


            string info = processSample(ref uProcessed);

            Sample sample = ConvertToSample(uProcessed.ToManagedImage(), 10);

            if (network != null)
            {
                network.Predict(sample);
                result = sample.recognizedClass;
                Console.WriteLine($"Распознано: {sample.recognizedClass}");
            }

            processed = uProcessed.ToManagedImage();

            return true;
        }

        /// <summary>
        /// Обработка одного сэмпла
        /// </summary>
        /// <param name="index"></param>
        private string processSample(ref AForge.Imaging.UnmanagedImage unmanaged)
        {
            string rez = "Обработка";

            ///  Инвертируем изображение
            AForge.Imaging.Filters.Invert InvertFilter = new AForge.Imaging.Filters.Invert();
            InvertFilter.ApplyInPlace(unmanaged);

            ///    Создаём BlobCounter, выдёргиваем самый большой кусок, масштабируем, пересечение и сохраняем
            ///    изображение в эксклюзивном использовании
            AForge.Imaging.BlobCounterBase bc = new AForge.Imaging.BlobCounter();

            bc.FilterBlobs = true;
            bc.MinWidth = 3;
            bc.MinHeight = 3;
            // Упорядочиваем по размеру
            bc.ObjectsOrder = AForge.Imaging.ObjectsOrder.Size;

            // Обрабатываем картинку            
            bc.ProcessImage(unmanaged);

            Rectangle[] rects = bc.GetObjectsRectangles();

            Rectangle biggestBlob = rects[0];
            int padding = 10; 
            int x = System.Math.Max(0, biggestBlob.X - padding);
            int y = System.Math.Max(0, biggestBlob.Y - padding);
            int width = System.Math.Min(unmanaged.Width - x, biggestBlob.Width + 2 * padding);
            int height = System.Math.Min(unmanaged.Height - y, biggestBlob.Height + 2 * padding);

            // Обрезаем с небольшими отступами
            AForge.Imaging.Filters.Crop cropFilter = new AForge.Imaging.Filters.Crop(new Rectangle(x, y, width, height));
            //  Масштабируем до 100x100
            AForge.Imaging.Filters.ResizeBilinear scaleFilter = new AForge.Imaging.Filters.ResizeBilinear(100, 100);
            unmanaged = scaleFilter.Apply(unmanaged);



            return rez;
        }

    }
}

