
using AForge.Imaging;
using AForge.Imaging.Filters;
using AIMLbot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace AIMLTGBot
{
	internal class MagicEye
    {

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
            

            string info = processSample(ref uProcessed);

            Sample sample = ConvertToSample(uProcessed.ToManagedImage(), 10);

            if (network != null)
            {
                network.Predict(sample);
                result = sample.recognizedClass;
                Console.WriteLine($"Распознано: {sample.recognizedClass}");
            }

            return true;
        }

        /// <summary>
        /// Обработка одного сэмпла
        /// </summary>
        /// <param name="index"></param>
        private string processSample(ref UnmanagedImage unmanaged)
        {
            string rez = "Обработка";

            ///  Инвертируем изображение
            Invert InvertFilter = new Invert();
            InvertFilter.ApplyInPlace(unmanaged);

            ///    Создаём BlobCounter, выдёргиваем самый большой кусок, масштабируем, пересечение и сохраняем
            ///    изображение в эксклюзивном использовании
            BlobCounterBase bc = new BlobCounter();

            bc.FilterBlobs = true;
            bc.MinWidth = 3;
            bc.MinHeight = 3;
            // Упорядочиваем по размеру
            bc.ObjectsOrder = AForge.Imaging.ObjectsOrder.Size;

            // Обрабатываем картинку            
            bc.ProcessImage(unmanaged);
            Rectangle[] rects = bc.GetObjectsRectangles();

			if (rects.Length == 0)
			{
				return "Blobs не найдены";
			}

			Rectangle biggestBlob = rects[0];
            int padding = 100; 
            int x = Math.Max(0, biggestBlob.X - padding);
            int y = Math.Max(0, biggestBlob.Y - padding);
            int width = Math.Min(unmanaged.Width - x, biggestBlob.Width + 2 * padding);
            int height = Math.Min(unmanaged.Height - y, biggestBlob.Height + 2 * padding);

            // Обрезаем с небольшими отступами
            Crop cropFilter = new Crop(new Rectangle(x, y, width, height));
			unmanaged = cropFilter.Apply(unmanaged);

			//  Масштабируем до 100x100
			ResizeBilinear scaleFilter = new ResizeBilinear(100, 100);
            unmanaged = scaleFilter.Apply(unmanaged);

            return rez;
        }

    }
}

