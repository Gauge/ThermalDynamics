using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Thermodynamics.Tests
{
    public class ThermodynamicsClientTests
    {

        private static string RepoRoot()
        {
            return Thermodynamics.Harness.ShippedBlocks.RepoRoot();
        }


        private static string ReadFunc(string source, int from)
        {
            int start = source.IndexOf("Func<", from, StringComparison.Ordinal);
            if (start < 0) return null;

            int open = source.IndexOf('<', start);
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '<') depth++;

                else if (source[i] == '>')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return Normalise("Func" + source.Substring(open, i - open + 1));
                    }
                }
            }
            return null;
        }


        private static string Normalise(string signature)
        {
            return Regex.Replace(signature, @"\s+", "");
        }


        private static Dictionary<string, string> ApiTable()
        {
            string source = File.ReadAllText(Path.Combine(RepoRoot(), "Thermodynamics", "ThermalApi.cs"));

            Dictionary<string, string> shapes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(source, @"methods\[""(\w+)""\]"))
            {

                string signature = ReadFunc(source, match.Index);
                if (signature != null) shapes[match.Groups[1].Value] = signature;
            }
            return shapes;
        }


        private static Dictionary<string, string> ClientBindings()
        {
            string source = File.ReadAllText(Path.Combine(RepoRoot(),
                "tools", "modkit", "ThermodynamicsApi.cs"));

            Dictionary<string, string> shapes = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Match match in Regex.Matches(source, @"Get<"))
            {

                string signature = ReadFunc(source, match.Index);
                if (signature == null) continue;

                Match name = Regex.Match(source.Substring(match.Index),
                    @"Get<.*?>\s*\(\s*table\s*,\s*""(\w+)""\s*\)", RegexOptions.Singleline);
                if (name.Success) shapes[name.Groups[1].Value] = signature;
            }

            return shapes;
        }

        [Fact]

        public void TheClientBindsEveryApiKeyWithTheSameSignature()
        {

            Dictionary<string, string> api = ApiTable();

            Dictionary<string, string> client = ClientBindings();

            Assert.True(api.Count >= 15,
                "only " + api.Count + " API keys were read, so this test is reading nothing");
            Assert.True(client.Count >= 15,
                "only " + client.Count + " client bindings were read, so the client's Get<> pattern"
                + " changed and this test no longer sees it");


            List<string> problems = new List<string>();

            foreach (KeyValuePair<string, string> entry in api)
            {
                string bound;
                if (!client.TryGetValue(entry.Key, out bound))
                {
                    problems.Add(entry.Key + ": the API publishes it and the client does not bind it");
                    continue;
                }
                if (bound != entry.Value)
                {
                    problems.Add(entry.Key + ":\n      api    " + entry.Value + "\n      client " + bound);
                }
            }

            foreach (KeyValuePair<string, string> entry in client)
            {
                if (!api.ContainsKey(entry.Key))
                {
                    problems.Add(entry.Key + ": the client binds it and the API does not publish it (cast will return null)");
                }
            }

            problems.Sort(StringComparer.Ordinal);
            Assert.True(problems.Count == 0,
                "the drop-in client and the API table disagree about " + problems.Count
                + " keys:\n  " + string.Join("\n  ", problems.ToArray()));
        }

        [Fact]

        public void TheClientChannelAndVersionMatchTheMod()
        {
            string apiSource = File.ReadAllText(Path.Combine(RepoRoot(),
                "Thermodynamics", "ThermalApi.cs"));
            string clientSource = File.ReadAllText(Path.Combine(RepoRoot(),
                "tools", "modkit", "ThermodynamicsApi.cs"));

            string apiChannel = Regex.Match(apiSource, @"ChannelId\s*=\s*(\d+)").Groups[1].Value;
            string clientChannel = Regex.Match(clientSource, @"Channel\s*=\s*(\d+)").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(apiChannel), "could not read the API channel id");
            Assert.Equal(apiChannel, clientChannel);

            string apiVersion = Regex.Match(apiSource, @"public\s+const\s+int\s+Version\s*=\s*(\d+)").Groups[1].Value;
            string clientMajor = Regex.Match(clientSource, @"SupportedMajor\s*=\s*(\d+)").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(apiVersion), "could not read the API version");
            Assert.Equal(apiVersion, clientMajor);
        }
    }
}
