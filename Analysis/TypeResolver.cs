namespace FireURQ.Porter.Analysis;

public sealed record TypeResolution(
    string DeclaredName,
    string ResolvedName,
    int PointerDepth,
    IReadOnlyList<string> Chain,
    bool HasCycle)
{
    public bool IsPointer => PointerDepth > 0;
    public bool IsAmbiguous { get; init; }
    public string? DeclaringUnitName { get; init; }
    public bool IsResolved => !HasCycle && !IsAmbiguous;
}

public sealed class TypeResolver
{
    private sealed record Declaration(SemanticUnit Unit, SemanticType Type);

    private readonly Dictionary<string, SemanticUnit> _units;
    private readonly Dictionary<string, List<Declaration>> _types;

    public TypeResolver(IEnumerable<SemanticUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        _units = new(StringComparer.OrdinalIgnoreCase);
        _types = new(StringComparer.OrdinalIgnoreCase);

        foreach (SemanticUnit unit in units)
        {
            if (!_units.TryAdd(unit.Name, unit))
                throw new ArgumentException(
                    $"Duplicate Delphi unit name: {unit.Name}",
                    nameof(units));

            foreach (SemanticType type in unit.Types)
            {
                if (!_types.TryGetValue(type.Name, out var declarations))
                {
                    declarations = new List<Declaration>();
                    _types.Add(type.Name, declarations);
                }

                declarations.Add(new Declaration(unit, type));
            }

            // Classes and interfaces are named types too. Keep the spelling
            // from their declarations when emitting case-sensitive C#.
            foreach (SemanticClass classType in unit.Classes)
            {
                if (!_types.TryGetValue(classType.Name, out var declarations))
                {
                    declarations = new List<Declaration>();
                    _types.Add(classType.Name, declarations);
                }

                if (!declarations.Any(declaration =>
                    declaration.Unit.Name.Equals(
                        unit.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    declarations.Add(new Declaration(
                        unit,
                        new SemanticType(
                            classType.Name, "Class", string.Empty,
                            UnitName: unit.Name)));
                }
            }

            foreach (SemanticInterface interfaceType in unit.Interfaces)
            {
                if (!_types.TryGetValue(interfaceType.Name, out var declarations))
                {
                    declarations = new List<Declaration>();
                    _types.Add(interfaceType.Name, declarations);
                }

                if (!declarations.Any(declaration =>
                    declaration.Unit.Name.Equals(
                        unit.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    declarations.Add(new Declaration(
                        unit,
                        new SemanticType(
                            interfaceType.Name, "Interface", string.Empty,
                            UnitName: unit.Name)));
                }
            }
        }
    }

    // Compatibility entry point. A unique declaration supplies its own context.
    public TypeResolution Resolve(string typeName) =>
        Resolve(typeName, null);

    public TypeResolution Resolve(string typeName, string? unitName)
    {
        string declaredName = typeName ?? string.Empty;
        string current = declaredName.Trim();
        string? context = unitName;
        string? declaringUnitName = null;
        int pointerDepth = 0;
        var chain = new List<string>();
        var visited = new HashSet<Declaration>();

        while (current.Length > 0)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                current,
                @"^[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)+$"))
            {
                current = System.Text.RegularExpressions.Regex.Replace(
                    current, @"\s*\.\s*", ".");
            }

            if (current.StartsWith("^", StringComparison.Ordinal))
            {
                pointerDepth++;
                current = current[1..].Trim();
                continue;
            }

            Declaration? declaration = Find(current, context, out bool ambiguous);

            if (ambiguous)
            {
                return new TypeResolution(
                    declaredName, current, pointerDepth, chain, false)
                {
                    IsAmbiguous = true
                };
            }

            if (declaration is null)
            {
                string? standardTarget = current.ToLowerInvariant() switch
                {
                    "pointer" => "void",
                    "pchar" or "pwidechar" => "Char",
                    "pansichar" => "AnsiChar",
                    "pbyte" => "Byte",
                    "plongword" => "Longword",
                    "pguid" => "TGUID",
                    "ppoint" => "TPoint",
                    "pword" => "Word",
                    _ => null
                };

                if (standardTarget is not null)
                {
                    pointerDepth++;
                    current = standardTarget;
                }

                break;
            }

            chain.Add(declaration.Type.Name);

            if (!visited.Add(declaration))
            {
                return new TypeResolution(
                    declaredName, current, pointerDepth, chain, true);
            }

            SemanticType type = declaration.Type;

            bool isPointer = type.Kind.Equals(
                "Pointer", StringComparison.OrdinalIgnoreCase);
            bool isAlias = type.Kind.Equals(
                "Simple", StringComparison.OrdinalIgnoreCase);

            if (!isPointer && !isAlias)
            {
                current = type.Name;
                declaringUnitName = declaration.Unit.Name;
                break;
            }

            string target = type.Definition.Trim();
            if (target.Length == 0)
                break;

            if (isPointer)
            {
                pointerDepth++;

                // Accept either parser representation: Integer or ^Integer.
                if (target.StartsWith("^", StringComparison.Ordinal))
                    target = target[1..].Trim();
            }

            context = declaration.Unit.Name;
            current = target;
        }

        return new TypeResolution(
            declaredName, current, pointerDepth, chain, false)
        {
            DeclaringUnitName = declaringUnitName
        };
    }

    // Classify a resolved Delphi declaration without guessing from its name.
    // Unknown includes missing, ambiguous, cyclic and pointer types.
    // Resolve only directly declared indexed properties.
    // Inherited and implicit default properties are handled separately.
    public bool TryResolveIndexedProperty(
        string receiverType,
        string propertyName,
        string? unitName,
        out SemanticMember property)
    {
        property = null!;

        if (string.IsNullOrWhiteSpace(receiverType) ||
            string.IsNullOrWhiteSpace(propertyName))
            return false;

        TypeResolution resolution = Resolve(receiverType, unitName);

        if (!resolution.IsResolved ||
            resolution.IsPointer ||
            resolution.DeclaringUnitName is null)
            return false;

        Declaration? declaration = Find(
            resolution.ResolvedName,
            resolution.DeclaringUnitName,
            out bool ambiguous);

        if (ambiguous || declaration is null)
            return false;

        SemanticUnit unit = declaration.Unit;

        IReadOnlyList<SemanticMember> members;

        if (declaration.Type.Kind.Equals(
            "Class", StringComparison.OrdinalIgnoreCase))
        {
            SemanticClass[] matches = unit.Classes
                .Where(candidate => candidate.Name.Equals(
                    declaration.Type.Name,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matches.Length != 1)
                return false;

            members = matches[0].Members;
        }
        else if (declaration.Type.Kind.Equals(
            "Interface", StringComparison.OrdinalIgnoreCase))
        {
            SemanticInterface[] matches = unit.Interfaces
                .Where(candidate => candidate.Name.Equals(
                    declaration.Type.Name,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matches.Length != 1)
                return false;

            members = matches[0].Members;
        }
        else
        {
            return false;
        }

        SemanticMember[] properties = members
            .Where(candidate =>
                candidate.Kind == SemanticMemberKind.Property &&
                candidate.Parameters.Count > 0 &&
                candidate.Name.Equals(
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (properties.Length != 1)
            return false;

        property = properties[0];
        return true;
    }

    public string ResolveDeclarationKind(
        string typeName,
        string? unitName = null)
    {
        TypeResolution resolution = Resolve(typeName, unitName);

        if (!resolution.IsResolved ||
            resolution.IsPointer ||
            resolution.DeclaringUnitName is null)
        {
            return "Unknown";
        }

        Declaration? declaration = Find(
            resolution.ResolvedName,
            resolution.DeclaringUnitName,
            out bool ambiguous);

        if (ambiguous || declaration is null)
            return "Unknown";

        return declaration.Type.Kind switch
        {
            "Class" => "Class",
            "Interface" => "Interface",
            _ => "Unknown"
        };
    }

    public bool TryResolvePointer(
        string typeName,
        out TypeResolution resolution)
    {
        resolution = Resolve(typeName);
        return resolution.IsResolved && resolution.IsPointer;
    }

    public bool TryResolvePointer(
        string typeName,
        string unitName,
        out TypeResolution resolution)
    {
        resolution = Resolve(typeName, unitName);
        return resolution.IsResolved && resolution.IsPointer;
    }

    public bool IsInheritedTStreamOperation(
        string typeName, string operation, string? unitName)
    {
        string current = typeName;
        string? context = unitName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!string.IsNullOrWhiteSpace(current))
        {
            TypeResolution resolution = Resolve(current, context);
            if (!resolution.IsResolved || resolution.IsPointer)
                return false;

            string name = resolution.ResolvedName;
            string? owner = resolution.DeclaringUnitName;
            if (owner is null)
            {
                if (name.StartsWith("Classes.", StringComparison.OrdinalIgnoreCase))
                    name = name["Classes.".Length..];
                return name.Equals("TStream", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("TMemoryStream", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("TFileStream", StringComparison.OrdinalIgnoreCase);
            }

            if (!visited.Add(owner + "." + name) ||
                !_units.TryGetValue(owner, out SemanticUnit? unit))
                return false;

            SemanticClass? classType = unit.Classes.FirstOrDefault(candidate =>
                candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (classType is null ||
                classType.Members.Any(member =>
                    member.Name.Equals(operation, StringComparison.OrdinalIgnoreCase)))
                return false;

            current = classType.BaseType ?? string.Empty;
            context = owner;
        }

        return false;
    }

    internal SemanticField? FindRecordField(string receiver, string name, string? unitName)
    {
        var type = Resolve(receiver, unitName);
        string recordName = type.ResolvedName;
        string? runtimeFieldType = recordName.ToUpperInvariant() switch
        {
            "TPOINT" when name.Equals("X", StringComparison.OrdinalIgnoreCase) ||
                           name.Equals("Y", StringComparison.OrdinalIgnoreCase) => "Integer",
            "TRECT" when name.ToUpperInvariant() is "LEFT" or "TOP" or "RIGHT" or "BOTTOM" => "Integer",
            "TRECT" when name.Equals("TopLeft", StringComparison.OrdinalIgnoreCase) ||
                         name.Equals("BottomRight", StringComparison.OrdinalIgnoreCase) => "TPoint",
            "D3DPRESENT_PARAMETERS" when name.ToUpperInvariant() is
                "BACKBUFFERWIDTH" or "BACKBUFFERHEIGHT" or "BACKBUFFERCOUNT" or "FLAGS" or
                "FULLSCREEN_REFRESHRATEINHZ" or "FULLSCREEN_PRESENTATIONINTERVAL" => "Cardinal",
            "D3DPRESENT_PARAMETERS" when name.ToUpperInvariant() is
                "WINDOWED" or "ENABLEAUTODEPTHSTENCIL" => "Boolean",
            "D3DPRESENT_PARAMETERS" when name.Equals("hDeviceWindow", StringComparison.OrdinalIgnoreCase) => "HWND",
            "D3DPRESENT_PARAMETERS" => "Integer",
            _ => null
        };
        if (runtimeFieldType is not null)
            return new SemanticField(name, runtimeFieldType);

        if (type.DeclaringUnitName is null)
            return null;
        var declaration = Find(type.ResolvedName, type.DeclaringUnitName, out _);
        var record = declaration?.Type;
        return record?.Fields?.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? record?.VariantPart?.Variants.SelectMany(v => v.Fields)
                .FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    internal TypeResolution? ResolveDataMemberType(string receiver, string memberName, string? unitName) =>
        FindMember(receiver, memberName, unitName, null) is { } data
            ? Resolve(data.Member.Type, data.UnitName)
            : FindRecordField(receiver, memberName, unitName) is { } field
                ? Resolve(field.Type, Resolve(receiver, unitName).DeclaringUnitName)
                : null;

    internal (string Name, TypeResolution Type)? ResolveFunctionMember(
        string receiver, string memberName, string? unitName, int argumentCount) =>
        FindMember(receiver, memberName, unitName, argumentCount) is { } method &&
        !string.IsNullOrWhiteSpace(method.Member.Type)
            ? (method.Member.Name, Resolve(method.Member.Type, method.UnitName)) : null;

    internal (SemanticMember Member, string UnitName)? FindMember(
        string receiver, string memberName, string? unitName, int? argumentCount, bool constructor = false)
    {
        string? context = unitName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrWhiteSpace(receiver))
        {
            TypeResolution type = Resolve(receiver, context);
            if (!type.IsResolved || type.IsPointer || type.DeclaringUnitName is not { } owner ||
                !visited.Add(owner + "." + type.ResolvedName) ||
                !_units.TryGetValue(owner, out SemanticUnit? unit))
                return null;

            var classType = unit.Classes.FirstOrDefault(candidate =>
                candidate.Name.Equals(type.ResolvedName, StringComparison.OrdinalIgnoreCase));
            var interfaceType = classType is null ? unit.Interfaces.FirstOrDefault(candidate =>
                candidate.Name.Equals(type.ResolvedName, StringComparison.OrdinalIgnoreCase)) : null;
            var members = classType?.Members ?? interfaceType?.Members;
            if (members is not null)
            {
                var named = members.Where(candidate =>
                    candidate.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (named.Length > 0)
                {
                    var candidates = named.Where(member =>
                        (!argumentCount.HasValue ||
                         AcceptsArity(member.Parameters, argumentCount.Value)) &&
                        (argumentCount.HasValue
                            ? member.Kind == SemanticMemberKind.Method &&
                              member.IsConstructor == constructor && !member.IsDestructor
                            : member.Kind is SemanticMemberKind.Field or SemanticMemberKind.Property)).ToArray();
                    return candidates.Length == 1
                        ? (candidates[0], owner)
                        : null;
                }
            }

            receiver = classType?.BaseType ?? interfaceType?.BaseType ?? string.Empty;
            context = owner;
        }

        return null;
    }

    private static bool AcceptsArity(
        IReadOnlyList<SemanticParameter> parameters, int supplied) =>
        supplied <= parameters.Count &&
        parameters.Skip(supplied).All(parameter =>
            !string.IsNullOrWhiteSpace(parameter.DefaultValue));

    public bool IsInheritedTObjectFree(string typeName, string? unitName)
    {
        string current = typeName;
        string? context = unitName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!string.IsNullOrWhiteSpace(current))
        {
            TypeResolution resolution = Resolve(current, context);
            if (!resolution.IsResolved || resolution.IsPointer)
                return false;

            string name = resolution.ResolvedName;
            string? owner = resolution.DeclaringUnitName;

            if (owner is null)
                return name.Equals("TObject", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("System.TObject", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("TInterfacedObject", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("System.TInterfacedObject", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("TStream", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("TMemoryStream", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("TFileStream", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Classes.TStream", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Classes.TMemoryStream", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Classes.TFileStream", StringComparison.OrdinalIgnoreCase);

            if (!visited.Add(owner + "." + name) ||
                !_units.TryGetValue(owner, out SemanticUnit? unit))
                return false;

            SemanticClass? classType = unit.Classes.FirstOrDefault(candidate =>
                candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (classType is null)
                return false;

            // Any explicit member with this name requires separate binding.
            // Do not replace user methods, fields, properties or delegates.
            if (classType.Members.Any(member =>
                member.Name.Equals("Free", StringComparison.OrdinalIgnoreCase)))
                return false;

            if (string.IsNullOrWhiteSpace(classType.BaseType))
                return true;

            current = classType.BaseType;
            context = owner;
        }

        return false;
    }

    public bool HasVisibleNonTypeDeclaration(string name, string unitName)
    {
        if (!_units.TryGetValue(unitName, out SemanticUnit? context))
            return true;

        var visible = new HashSet<string>(
            context.Uses, StringComparer.OrdinalIgnoreCase) { context.Name };

        return _units.Values.Where(unit => visible.Contains(unit.Name))
            .Any(unit =>
                unit.Constants.Any(value => value.Name.Equals(
                    name, StringComparison.OrdinalIgnoreCase)) ||
                unit.Variables.Any(value => value.Name.Equals(
                    name, StringComparison.OrdinalIgnoreCase)) ||
                unit.Functions.Any(value => value.Name.Equals(
                    name, StringComparison.OrdinalIgnoreCase)));
    }
    internal IReadOnlyList<SemanticParameter>? FindUnitRoutineParameters(string name, string? unitName, int arity)
    {
        if (unitName is null || !_units.TryGetValue(unitName, out var unit))
            return null;

        var matches = unit.Functions.Where(function =>
            AcceptsArity(function.Parameters, arity) &&
            function.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length == 0)
            return null;
        var parameters = matches
            .OrderByDescending(match => match.Parameters.Count(parameter =>
                !string.IsNullOrWhiteSpace(parameter.DefaultValue)))
            .First().Parameters;
        // The interface declaration and implementation may both be present.
        return matches.All(match => match.Parameters.Zip(parameters).All(pair =>
            pair.First.Kind.Equals(pair.Second.Kind, StringComparison.OrdinalIgnoreCase) &&
            pair.First.Type.Equals(pair.Second.Type, StringComparison.OrdinalIgnoreCase)))
            ? parameters : null;
    }

    internal SemanticProcedureType? FindUnitProcedureVariable(string name, string? unitName)
    {
        if (unitName is null || !_units.TryGetValue(unitName, out var unit))
            return null;

        return unit.Variables.FirstOrDefault(variable =>
            variable.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.ProcedureType;
    }

    public bool HasVisibleValueDeclaration(string name, string unitName)
    {
        if (!_units.TryGetValue(unitName, out SemanticUnit? context))
            return true;

        var visible = new HashSet<string>(
            context.Uses, StringComparer.OrdinalIgnoreCase) { context.Name };

        return _units.Values.Where(unit => visible.Contains(unit.Name))
            .Any(unit =>
                unit.Constants.Any(value => value.Name.Equals(
                    name, StringComparison.OrdinalIgnoreCase)) ||
                unit.Variables.Any(value => value.Name.Equals(
                    name, StringComparison.OrdinalIgnoreCase)) ||
                unit.Functions.Any(value => value.Name.Equals(
                    name, StringComparison.OrdinalIgnoreCase)) ||
                unit.Types.Any(value => value.Name.Equals(
                    name, StringComparison.OrdinalIgnoreCase)) ||
                unit.Classes.Any(value => value.Name.Equals(
                    name, StringComparison.OrdinalIgnoreCase)) ||
                unit.Interfaces.Any(value => value.Name.Equals(
                    name, StringComparison.OrdinalIgnoreCase)));
    }
    public bool TryResolveConstant(
        string name,
        string? unitName,
        out SemanticConstant? constant,
        out string? declaringUnit)
    {
        constant = null;
        declaringUnit = null;
        name = System.Text.RegularExpressions.Regex.Replace(
            name.Trim(), @"\s*\.\s*", ".");

        IEnumerable<SemanticUnit> candidates;
        int separator = name.LastIndexOf('.');

        if (separator > 0)
        {
            string owner = name[..separator];
            name = name[(separator + 1)..];
            candidates = _units.Values.Where(unit =>
                unit.Name.Equals(owner, StringComparison.OrdinalIgnoreCase));
        }
        else if (unitName is not null)
        {
            if (!_units.TryGetValue(unitName, out SemanticUnit? context))
                return false;

            var local = context.Constants.Where(value =>
                value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (local.Length > 0)
            {
                if (local.Length != 1)
                    return false;

                constant = local[0];
                declaringUnit = context.Name;
                return true;
            }

            var imports = new HashSet<string>(
                context.Uses, StringComparer.OrdinalIgnoreCase);
            candidates = _units.Values.Where(unit => imports.Contains(unit.Name));
        }
        else
        {
            candidates = _units.Values;
        }

        foreach (SemanticUnit unit in candidates)
        {
            foreach (SemanticConstant value in unit.Constants)
            {
                if (!value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (constant is not null)
                {
                    constant = null;
                    declaringUnit = null;
                    return false;
                }

                constant = value;
                declaringUnit = unit.Name;
            }
        }

        return constant is not null;
    }
    public bool TryGetCompositeDefinition(
        string typeName,
        string? unitName,
        out string definition,
        out string? declaringUnit)
    {
        definition = string.Empty;
        declaringUnit = null;

        Declaration? declaration = Find(typeName, unitName, out bool ambiguous);

        if (ambiguous || declaration is null)
            return false;

        if (!declaration.Type.Kind.Equals(
                "Array", StringComparison.OrdinalIgnoreCase) &&
            !declaration.Type.Kind.Equals(
                "Set", StringComparison.OrdinalIgnoreCase))
            return false;

        definition = declaration.Type.Definition.Trim();
        declaringUnit = declaration.Unit.Name;
        return definition.Length > 0;
    }

    private Declaration? Find(
        string name,
        string? context,
        out bool ambiguous)
    {
        ambiguous = false;

        // A qualified Delphi type explicitly identifies its declaring unit.
        int separator = name.LastIndexOf('.');
        if (separator > 0)
        {
            return Select(
                name[(separator + 1)..],
                declaration => declaration.Unit.Name.Equals(
                    name[..separator], StringComparison.OrdinalIgnoreCase),
                out ambiguous);
        }

        if (context is null)
            return Select(name, _ => true, out ambiguous);

        Declaration? local = Select(
            name,
            declaration => declaration.Unit.Name.Equals(
                context, StringComparison.OrdinalIgnoreCase),
            out ambiguous);

        if (local is not null || ambiguous)
            return local;

        if (!_units.TryGetValue(context, out SemanticUnit? unit))
            return null;

        // Keep multiple imported candidates explicit until uses precedence
        // and interface/implementation visibility are modeled precisely.
        var imports = new HashSet<string>(
            unit.Uses, StringComparer.OrdinalIgnoreCase);

        Declaration? imported = Select(
            name,
            declaration => imports.Contains(declaration.Unit.Name),
            out ambiguous);

        if (!ambiguous)
            return imported;

        if (!_types.TryGetValue(name, out var declarations))
            return null;

        var candidates = declarations.Where(declaration =>
            imports.Contains(declaration.Unit.Name)).ToArray();

        Declaration? commonOrigin = null;
        foreach (Declaration candidate in candidates)
        {
            if (!TryGetReexportOrigin(candidate, out Declaration origin))
                return null;

            if (commonOrigin is null)
                commonOrigin = origin;
            else if (!commonOrigin.Equals(origin))
                return null;
        }

        if (candidates.Length == 0)
            return null;

        ambiguous = false;
        return candidates[0];
    }

    private bool TryGetReexportOrigin(
        Declaration declaration,
        out Declaration origin)
    {
        origin = declaration;
        var visited = new HashSet<Declaration>();

        while (visited.Add(origin))
        {
            if (!origin.Type.Kind.Equals(
                    "Simple", StringComparison.OrdinalIgnoreCase))
                return true;

            string target = System.Text.RegularExpressions.Regex.Replace(
                origin.Type.Definition.Trim(), @"\s*\.\s*", ".");

            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    target, @"^[\p{L}_][\p{L}\p{Nd}_]*(?:\.[\p{L}_][\p{L}\p{Nd}_]*)?$"))
                return true;

            int separator = target.LastIndexOf('.');
            string owner = separator > 0
                ? target[..separator]
                : origin.Unit.Name;
            string name = separator > 0
                ? target[(separator + 1)..]
                : target;

            Declaration? next = Select(
                name,
                candidate => candidate.Unit.Name.Equals(
                    owner, StringComparison.OrdinalIgnoreCase),
                out bool ambiguous);

            if (ambiguous)
                return false;

            // Keep unresolved terminals attached to their declaration identity.
            // Independent aliases to the same primitive are not merged.
            if (next is null)
                return true;

            origin = next;
        }

        return false;
    }
    private Declaration? Select(
        string name,
        Func<Declaration, bool> predicate,
        out bool ambiguous)
    {
        ambiguous = false;
        Declaration? result = null;

        if (!_types.TryGetValue(name, out var declarations))
            return null;

        foreach (Declaration declaration in declarations)
        {
            if (!predicate(declaration))
                continue;

            if (result is not null)
            {
                ambiguous = true;
                return null;
            }

            result = declaration;
        }

        return result;
    }
}
