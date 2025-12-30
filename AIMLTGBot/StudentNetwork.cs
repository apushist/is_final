using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AIMLTGBot
{
	public delegate void TrainProgressHandler(double progress, double error, TimeSpan time);

	public class StudentNetwork 
    {
		private int[] structure;
		private double[][] neurons;
		private double[][][] weights;
		private double[][] biases;

		public Stopwatch stopWatch = new Stopwatch();

		public StudentNetwork()
		{
			using (var reader = new BinaryReader(File.Open(GetProjectRoot() + "\\networks\\studentNetF01.bin", FileMode.Open)))
			{
				int structureLength = reader.ReadInt32();
				structure = new int[structureLength];
				for (int i = 0; i < structureLength; i++)
					structure[i] = reader.ReadInt32();

				int neuronsLength = reader.ReadInt32();
				neurons = new double[neuronsLength][];
				for (int i = 0; i < neuronsLength; i++)
				{
					int layerSize = reader.ReadInt32();
					neurons[i] = new double[layerSize];
					for (int j = 0; j < layerSize; j++)
						neurons[i][j] = reader.ReadDouble();
				}

				int weightsLength = reader.ReadInt32();
				weights = new double[weightsLength][][];
				for (int i = 0; i < weightsLength; i++)
				{
					int layerLength = reader.ReadInt32();
					weights[i] = new double[layerLength][];
					for (int j = 0; j < layerLength; j++)
					{
						int neuronWeightsLength = reader.ReadInt32();
						weights[i][j] = new double[neuronWeightsLength];
						for (int k = 0; k < neuronWeightsLength; k++)
							weights[i][j][k] = reader.ReadDouble();
					}
				}

				int biasesLength = reader.ReadInt32();
				biases = new double[biasesLength][];
				for (int i = 0; i < biasesLength; i++)
				{
					int layerBiasesLength = reader.ReadInt32();
					biases[i] = new double[layerBiasesLength];
					for (int j = 0; j < layerBiasesLength; j++)
						biases[i][j] = reader.ReadDouble();
				}
			}

		}

		public static string GetProjectRoot()
		{
			string currentDir = Directory.GetCurrentDirectory();

			DirectoryInfo dir = new DirectoryInfo(currentDir);

			while (dir != null)
			{
				var csprojFiles = dir.GetFiles("*.csproj", SearchOption.TopDirectoryOnly);
				var slnFiles = dir.GetFiles("*.sln", SearchOption.TopDirectoryOnly);

				if (csprojFiles.Length > 0 || slnFiles.Length > 0)
				{
					return dir.FullName;
				}

				var srcDir = dir.GetDirectories("src", SearchOption.TopDirectoryOnly);
				var propertiesDir = dir.GetDirectories("Properties", SearchOption.TopDirectoryOnly);

				if (propertiesDir.Length > 0 || srcDir.Length > 0)
				{
					return dir.FullName;
				}

				dir = dir.Parent;
			}

			return Directory.GetCurrentDirectory();
		}

		private double Sigmoid(double x)
		{
			return 1.0 / (1.0 + System.Math.Exp(-x));
		}

		private double[] Compute(double[] input)
		{
			Array.Copy(input, neurons[0], input.Length);

			for (int layer = 0; layer < weights.Length; layer++)
			{
				Parallel.For(0, neurons[layer + 1].Length, neuron =>
				{
					double sum = biases[layer][neuron];

					for (int prevNeuron = 0; prevNeuron < neurons[layer].Length; prevNeuron++)
					{
						sum += neurons[layer][prevNeuron] * weights[layer][neuron][prevNeuron];
					}
					neurons[layer + 1][neuron] = Sigmoid(sum);
				});
			}

			return neurons[neurons.Length - 1];
		}

		/// <summary>
		/// Угадывает тип фигуры на основе результатов подсчётов сети.
		/// </summary>
		/// <param name="sample">Фигура, которую необходимо определить</param>
		/// <returns></returns>
		public FigureType Predict(Sample sample)
		{
			return sample.ProcessPrediction(Compute(sample.input));
		}

	}
}