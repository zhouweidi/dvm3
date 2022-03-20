using PerformanceTests.PoliteAnts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PerformanceTests
{
	class Program
	{
		static void Main(string[] _)
		{
			RunPoliteAnts();

			Console.WriteLine();
			Console.WriteLine("Test over");
		}

		#region Polite Ants

		static void RunPoliteAnts()
		{
			var items = GetTestItems(
				new[]
				{ 
					1, 
					2, 
					3, 
					4
				});

			var output = RunTests(items);

			var outputFileName = $"{Path.GetTempFileName()}.txt";
			WriteAndOpenFile(outputFileName, output);
		}

		static IReadOnlyList<(string name, TestCondition condition)> GetTestItems(IEnumerable<int> vmProcessorsCounts)
		{
			var items = new List<(string name, TestCondition condition)>();

			foreach (var processors in vmProcessorsCounts)
			{
				Debug.Assert(processors > 0);

				var condition = TestCondition.Default;

				condition.VmProcessorsCount = processors;

				items.Add(($"VmProcessor{processors}", condition));
			}

			return items;
		}

		static string RunTests(IReadOnlyList<(string name, TestCondition condition)> items)
		{
			var output = new StringBuilder();

			for (int i = 0; i < items.Count; i++)
			{
				var (name, condition) = items[i];

				using var test = new PoliteAnts.PoliteAnts(condition, name);

				test.Run();
				test.Print("----------------------------------");

				output.Append(test.GetPrintContent());
			}

			return output.ToString();
		}

		#endregion

		#region Testing support

		static void WriteAndOpenFile(string fileName, string content)
		{
			File.WriteAllText(fileName, content);

			var proc = new Process
			{
				EnableRaisingEvents = false
			};

			proc.StartInfo.UseShellExecute = true;
			proc.StartInfo.FileName = fileName;
			proc.Start();

			proc.WaitForExit();
		}

		#endregion
	}
}
