using System;
using System.IO;
using System.Xml.Linq;

namespace DevStudio.Projects
{
    public static class CppProjectFactory
    {
        public static CppProject CreateNew(string vcxprojPath, string name)
        {
            vcxprojPath = Path.GetFullPath(vcxprojPath);
            Directory.CreateDirectory(Path.GetDirectoryName(vcxprojPath));
            var guid = Guid.NewGuid().ToString("B").ToUpperInvariant();
            var xml = string.Format(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Environment.NewLine +
                "<Project DefaultTargets=\"Build\" ToolsVersion=\"15.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">" + Environment.NewLine +
                "  <ItemGroup Label=\"ProjectConfigurations\">" + Environment.NewLine +
                "    <ProjectConfiguration Include=\"Debug|Win32\">" + Environment.NewLine +
                "      <Configuration>Debug</Configuration>" + Environment.NewLine +
                "      <Platform>Win32</Platform>" + Environment.NewLine +
                "    </ProjectConfiguration>" + Environment.NewLine +
                "    <ProjectConfiguration Include=\"Release|x64\">" + Environment.NewLine +
                "      <Configuration>Release</Configuration>" + Environment.NewLine +
                "      <Platform>x64</Platform>" + Environment.NewLine +
                "    </ProjectConfiguration>" + Environment.NewLine +
                "  </ItemGroup>" + Environment.NewLine +
                "  <PropertyGroup Label=\"Globals\">" + Environment.NewLine +
                "    <ProjectGuid>{0}</ProjectGuid>" + Environment.NewLine +
                "    <RootNamespace>{1}</RootNamespace>" + Environment.NewLine +
                "    <ProjectName>{1}</ProjectName>" + Environment.NewLine +
                "  </PropertyGroup>" + Environment.NewLine +
                "  <Import Project=\"$(VCTargetsPath)\\Microsoft.Cpp.Default.props\" />" + Environment.NewLine +
                "  <PropertyGroup Condition=\"'$(Configuration)|$(Platform)'=='Debug|Win32'\" Label=\"Configuration\">" + Environment.NewLine +
                "    <ConfigurationType>Application</ConfigurationType>" + Environment.NewLine +
                "    <PlatformToolset>v141</PlatformToolset>" + Environment.NewLine +
                "  </PropertyGroup>" + Environment.NewLine +
                "  <PropertyGroup Condition=\"'$(Configuration)|$(Platform)'=='Release|x64'\" Label=\"Configuration\">" + Environment.NewLine +
                "    <ConfigurationType>Application</ConfigurationType>" + Environment.NewLine +
                "    <PlatformToolset>v141</PlatformToolset>" + Environment.NewLine +
                "  </PropertyGroup>" + Environment.NewLine +
                "  <Import Project=\"$(VCTargetsPath)\\Microsoft.Cpp.props\" />" + Environment.NewLine +
                "  <PropertyGroup>" + Environment.NewLine +
                "    <TargetName>{1}</TargetName>" + Environment.NewLine +
                "    <OutDir>bin\\$(Platform)\\</OutDir>" + Environment.NewLine +
                "  </PropertyGroup>" + Environment.NewLine +
                "  <ItemGroup>" + Environment.NewLine +
                "    <ClCompile Include=\"main.cpp\" />" + Environment.NewLine +
                "  </ItemGroup>" + Environment.NewLine +
                "  <Import Project=\"$(VCTargetsPath)\\Microsoft.Cpp.targets\" />" + Environment.NewLine +
                "</Project>" + Environment.NewLine,
                guid, name);
            File.WriteAllText(vcxprojPath, xml);
            return new CppProject(vcxprojPath);
        }
    }
}
