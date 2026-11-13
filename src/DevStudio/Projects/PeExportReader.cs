using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection.PortableExecutable;

namespace DevStudio.Projects
{
    public static class PeExportReader
    {
        public static List<string> GetExportNames(string dllPath)
        {
            var names = new List<string>();
            using (var stream = File.OpenRead(dllPath))
            using (var pe = new PEReader(stream))
            {
                var headers = pe.PEHeaders;
                var header = headers.PEHeader;
                if (header == null || header.ExportTableDirectory.RelativeVirtualAddress == 0)
                {
                    return names;
                }
                var sections = new List<SectionData>();
                foreach (var section in headers.SectionHeaders)
                {
                    sections.Add(new SectionData
                    {
                        VirtualAddress = section.VirtualAddress,
                        Content = pe.GetSectionData(section.VirtualAddress).GetContent()
                    });
                }
                int directoryRva = header.ExportTableDirectory.RelativeVirtualAddress;
                int numberOfNames = ReadInt32(sections, directoryRva + 24);
                int addressOfNames = ReadInt32(sections, directoryRva + 32);
                int addressOfNameOrdinals = ReadInt32(sections, directoryRva + 36);
                for (int i = 0; i < numberOfNames; i++)
                {
                    int nameRva = ReadInt32(sections, addressOfNames + 4 * i);
                    if (nameRva == 0)
                    {
                        continue;
                    }
                    var name = ReadString(sections, nameRva);
                    if (!string.IsNullOrEmpty(name))
                    {
                        names.Add(name);
                    }
                }
            }
            return names;
        }

        private static int ReadInt32(List<SectionData> sections, int rva)
        {
            int index = FindIndex(sections, rva);
            var content = FindContent(sections, rva);
            return content[index]
                | (content[index + 1] << 8)
                | (content[index + 2] << 16)
                | (content[index + 3] << 24);
        }

        private static string ReadString(List<SectionData> sections, int rva)
        {
            int index = FindIndex(sections, rva);
            var content = FindContent(sections, rva);
            var chars = new List<char>();
            while (index < content.Length)
            {
                byte value = content[index];
                if (value == 0)
                {
                    break;
                }
                chars.Add((char)value);
                index++;
            }
            return new string(chars.ToArray());
        }

        private static int FindIndex(List<SectionData> sections, int rva)
        {
            foreach (var section in sections)
            {
                if (rva >= section.VirtualAddress && rva < section.VirtualAddress + section.Content.Length)
                {
                    return rva - section.VirtualAddress;
                }
            }
            throw new IOException("RVA 0x" + rva.ToString("X") + " is outside any section.");
        }

        private static ImmutableArray<byte> FindContent(List<SectionData> sections, int rva)
        {
            foreach (var section in sections)
            {
                if (rva >= section.VirtualAddress && rva < section.VirtualAddress + section.Content.Length)
                {
                    return section.Content;
                }
            }
            throw new IOException("RVA 0x" + rva.ToString("X") + " is outside any section.");
        }

        private sealed class SectionData
        {
            public int VirtualAddress;
            public ImmutableArray<byte> Content;
        }
    }
}
