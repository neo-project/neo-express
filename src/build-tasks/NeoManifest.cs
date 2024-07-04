// Copyright (C) 2015-2024 The Neo Project.
//
// NeoManifest.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.BuildTasks
{
    // Parse Manifest ABI JSON manually using SimpleJSON to avoid taking dependency on neo.dll or a JSON parsing package
    public class NeoManifest
    {
        public class Method
        {
            public string Name { get; set; } = "";
            public string ReturnType { get; set; } = "";
            public IReadOnlyList<(string Name, string Type)> Parameters { get; set; } = Array.Empty<(string Name, string Type)>();
        }

        public class Event
        {
            public string Name { get; set; } = "";
            public IReadOnlyList<(string Name, string Type)> Parameters { get; set; } = Array.Empty<(string Name, string Type)>();
        }

        public string Name { get; set; } = "";
        public IReadOnlyList<Method> Methods { get; set; } = Array.Empty<Method>();
        public IReadOnlyList<Event> Events { get; set; } = Array.Empty<Event>();

        public static NeoManifest Load(string manifestPath)
        {
            var text = File.ReadAllText(manifestPath) ?? throw new FileNotFoundException("", manifestPath);
            var json = SimpleJSON.JSON.Parse(text) ?? throw new InvalidOperationException();
            return NeoManifest.FromManifestJson(json);
        }

        public static NeoManifest FromManifestJson(SimpleJSON.JSONNode json)
        {
            var contractName = json["name"].Value;
            var abi = json["abi"];
            var methods = abi["methods"].Linq.Select(kvp => MethodFromJson(kvp.Value));
            var events = abi["events"].Linq.Select(kvp => EventFromJson(kvp.Value));

            return new NeoManifest
            {
                Name = contractName,
                Methods = methods.ToList(),
                Events = events.ToList()
            };
        }

        static (string Name, string Type) ParamFromJson(JSONNode json)
        {
            var name = json["name"].Value;
            var type = json["type"].Value;
            return (name, type);
        }

        static Method MethodFromJson(JSONNode json)
        {
            var name = json["name"].Value;
            var returnType = json["returntype"].Value;
            var @params = json["parameters"].Linq.Select(kvp => ParamFromJson(kvp.Value));
            return new Method
            {
                Name = name,
                ReturnType = returnType,
                Parameters = @params.ToList()
            };
        }

        static Event EventFromJson(JSONNode json)
        {
            var name = json["name"].Value;
            var @params = json["parameters"].Linq.Select(kvp => ParamFromJson(kvp.Value));
            return new Event
            {
                Name = name,
                Parameters = @params.ToList()
            };
        }
    }
}
