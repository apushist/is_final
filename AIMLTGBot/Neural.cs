using System;
using System.Collections.Generic;
using System.Collections;

namespace AIMLTGBot
{
	public enum FigureType : byte { Audi = 0, Citroen, Ford, Hyundai, Infiniti, 
        Mercedes, Mitsubishi, Opel, Renault, Toyota, Undefined
	};

	/// <summary>
	/// Класс для хранения образа – входной массив сигналов на сенсорах, выходные сигналы сети, и прочее
	/// </summary>
	public class Sample
    {
        /// <summary>
        /// Входной вектор
        /// </summary>
        public double[] input = null;

        /// <summary>
        /// Вектор ошибки, вычисляется по какой-нибудь хитрой формуле
        /// </summary>
        public double[] error = null;

        /// <summary>
        /// Распознанный класс - определяется после обработки
        /// </summary>
        public FigureType recognizedClass;

        /// <summary>
        /// Конструктор образа - на основе входных данных для сенсоров, при этом можно указать класс образа, или не указывать
        /// </summary>
        /// <param name="inputValues"></param>
        /// <param name="sampleClass"></param>
        public Sample(double[] inputValues, int classesCount)
        {
            input = (double[]) inputValues.Clone();
            Output = new double[classesCount];
            recognizedClass = FigureType.Undefined;
        }

        /// <summary>
        /// Выходной вектор, задаётся извне как результат распознавания
        /// </summary>
        public double[] Output { get; private set; }

        /// <summary>
        /// Обработка реакции сети на данный образ на основе вектора выходов сети
        /// </summary>
        public FigureType ProcessPrediction(double[] neuralOutput)
        {
            Output = neuralOutput;

            recognizedClass = (FigureType)10;
            for (int i = 0; i < Output.Length; ++i)
            {
                double outp = (int)recognizedClass == 10 ? 0.1: Output[(int)recognizedClass]; 
                if (Output[i] > outp) recognizedClass = (FigureType) i;
            }

            return recognizedClass;
        }

        /// <summary>
        /// Представление в виде строки
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string result = "Input : ";
            for (int i = 0; i < input.Length; ++i) result += input[i].ToString() + "; ";
            result += Environment.NewLine + "Output : ";
            if (Output == null) result += "null;";
            else
                for (int i = 0; i < Output.Length; ++i)
                    result += Output[i].ToString() + "; ";
            result += Environment.NewLine + "Error : ";

            if (error == null) result += "null;";
            else
                for (int i = 0; i < error.Length; ++i)
                    result += error[i].ToString() + "; ";
            result += Environment.NewLine + "Recognized : " + recognizedClass.ToString() + "(" +
                      ((int) recognizedClass).ToString() + "); " + Environment.NewLine;


            return result;
        }
    }

}