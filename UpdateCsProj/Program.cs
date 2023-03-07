using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace UpdateCsProj;

internal enum ProjectFileErrors
{
    None,
    NoProjectRoot,
    NoSdkAttribute,
    NoPropertyGroup
}

internal class Program
{
    private static void Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--help")
            {
                PrintUsageAndExit();
            }

            var files = GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
            if (files.Count > 3)
            {
                Console.Error.WriteLine($"Too many csproj files to process: the limit is three.");
                Environment.Exit(-1);
            }

            foreach (var file in files)
            {
                var error = UpdateCsprojFile(file);
                var message = error switch
                {
                    ProjectFileErrors.None => $"Updated file {file}",
                    ProjectFileErrors.NoProjectRoot => $"File {file} does not have <Project> as the root element",
                    ProjectFileErrors.NoSdkAttribute => $"File {file} has a <Project> root node but does not have an Sdk Attribute in the root node",
                    ProjectFileErrors.NoPropertyGroup => $"File {file} has a <Project> root node and Sdk attribute but does not have any PropertyGroup nodes",
                    _ => throw new NotImplementedException(),
                };
                Console.WriteLine(message);
            }


        }
        catch (Exception ex)
        {
            var fullname = System.Reflection.Assembly.GetEntryAssembly().Location;
            var progname = Path.GetFileNameWithoutExtension(fullname);
            Console.Error.WriteLine($"{progname} Error: {ex.Message}");
        }

    }

    private static List<string> GetFiles(string startDir, string pattern)
    {
        var result = new List<string>();
        var dirStack = new Stack<string>();
        dirStack.Push(startDir);

        while (dirStack.Count > 0)
        {
            var dir = dirStack.Pop();
            var files = Directory.GetFiles(dir, pattern);
            result.AddRange(files);

            var subDirs = Directory.GetDirectories(dir);
            foreach (var subDir in subDirs)
            {
                dirStack.Push(subDir);
            }
        }
        return result;
    }

    private static ProjectFileErrors UpdateCsprojFile(string csProjFile)
    {
        var doc = XDocument.Load(csProjFile);
        var rootName = doc.Root.Name.LocalName;
        var sdkAttribute = doc.Root.Attribute("Sdk");

        if (rootName != "Project")
        {
            return ProjectFileErrors.NoProjectRoot;
        }

        if (sdkAttribute == null)
        {
            return ProjectFileErrors.NoSdkAttribute;
        }

        var propGroup = doc.Descendants("PropertyGroup").FirstOrDefault();
        if (propGroup == default)
        {
            return ProjectFileErrors.NoPropertyGroup;
        }

        var frameworkElement = propGroup.Element("TargetFramework");

        if (frameworkElement == null)
        {
            propGroup.Add(new XElement("TargetFramework", @"net48"));
        }
        else
        {
            var currentFramework = frameworkElement.Value;

            // only update framework to net48 if we are on .net4 - otherwise leave it because it might be 5, 6, 7, 8:
            if (currentFramework.IndexOf("net4", StringComparison.OrdinalIgnoreCase) != -1)
            {
                frameworkElement.Value = "net48";
            }
        }

        var debugTypeElement = propGroup.Element("DebugType");
        if (debugTypeElement == null)
        {
            propGroup.Add(new XElement("DebugType", "embedded"));
        }
        else
        {
            debugTypeElement.Value = "embedded";
        }

        var langVersionElement = propGroup.Element("LangVersion");
        if (langVersionElement == null)
        {
            propGroup.Add(new XElement("LangVersion", "Latest"));
        }
        else
        {
            langVersionElement.Value = "Latest";
        }

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
        };

        using var writer = XmlWriter.Create(csProjFile, settings);
        doc.Save(writer);
        return ProjectFileErrors.None;
    }


    private static void PrintUsageAndExit()
    {
        Environment.Exit(-1);
    }
}
