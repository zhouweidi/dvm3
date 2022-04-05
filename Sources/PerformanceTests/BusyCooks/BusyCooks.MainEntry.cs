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
					//1 * 10000,
					//2 * 10000,
					//4 * 10000,
					6 * 10000,
					8 * 10000,
					10 * 10000,
				});

			var output = RunTests(items);

			var outputFileName = $"{Path.GetTempFileName()}.txt";
			WriteAndOpenFile(outputFileName, output);
		}

		static IReadOnlyList<(string name, TestCondition condition)> GetTestItems(IEnumerable<int> cooksCounts)
		{
			var items = new List<(string name, TestCondition condition)>();

			foreach (var cooks in cooksCounts)
			{
				Debug.Assert(cooks > 0);

				var condition = TestCondition.CreateDefault();

				// Change
				condition.CooksCount = cooks;

				items.Add(($"Cooks-{cooks:N0}", condition));
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

				test.Print("-----------------");

				output.Append(test.GetPrintContent());
			}

			return output.ToString();
		}
	}
}
