using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PerformanceTests.BusyCooks
{
	partial class BusyCooks
	{
		public static void MainEntry()
		{
			var items = GetTestItems(
				new[]
				{ 
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

				var condition = TestCondition.CreateDefault();

				// Change
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

				using var test = new BusyCooks(condition, name);

				test.Run();

				if (test.Inspector != null)
					PrintInspector(test);

				output.Append(test.GetPrintContent());
			}

			return output.ToString();
		}
	}
}
