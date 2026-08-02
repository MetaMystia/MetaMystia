using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MetaMystia.Debugger
{
    public class InspectionResult
    {
        public string Path { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Address { get; set; }
        public bool IsExpandable { get; set; }
        public List<MemberInfoDTO> Members { get; set; }
        public string Error { get; set; }
    }

    public class MemberInfoDTO
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Address { get; set; }
        public bool IsExpandable { get; set; }
    }

    public static class ReflectionEvaluator
    {
        public static List<object> FoundResources = new List<object>();
        public static Dictionary<string, object> SharedVariables = new Dictionary<string, object>
        {
            { "$debug", new DebugHelper() }
        };

        public static object FindResources(string className)
        {
            FoundResources.Clear();
            
            Type type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(className);
                if (type != null) break;
            }
            
            if (type == null)
            {
                throw new Exception($"Type '{className}' not found.");
            }

            var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(type);
            var objects = UnityEngine.Resources.FindObjectsOfTypeAll(il2cppType);
            
            var result = new List<object>();
            foreach (var obj in objects)
            {
                object typed;
                try { typed = Activator.CreateInstance(type, obj.Pointer); }
                catch { typed = obj; }

                FoundResources.Add(typed);
                result.Add(new { 
                    name = obj.name, 
                    address = GetAddress(obj),
                    className = className
                });
            }
            return result;
        }

        public static object FindResources(string className, string filterExpression)
        {
            if (string.IsNullOrWhiteSpace(filterExpression))
                return FindResources(className);

            FoundResources.Clear();

            Type type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(className);
                if (type != null) break;
            }

            if (type == null)
            {
                throw new Exception($"Type '{className}' not found.");
            }

            var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(type);
            var objects = UnityEngine.Resources.FindObjectsOfTypeAll(il2cppType);

            var result = new List<object>();
            foreach (var obj in objects)
            {
                object typed;
                try { typed = Activator.CreateInstance(type, obj.Pointer); }
                catch { typed = obj; }

                SharedVariables["$it"] = typed;
                try
                {
                    var evalResult = Evaluate(filterExpression);
                    bool pass = evalResult is true
                                || (evalResult is string s && !string.IsNullOrEmpty(s))
                                || (evalResult != null && evalResult is not false);
                    if (!pass) continue;
                }
                catch
                {
                    continue;
                }

                FoundResources.Add(typed);
                result.Add(new
                {
                    name = obj.name,
                    address = GetAddress(obj),
                    className = className
                });
            }

            SharedVariables.Remove("$it");
            return result;
        }

        public static object GetSceneHierarchy()
        {
            var scenesData = new List<object>();
            
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.IsValid()) continue;
                
                var rootObjects = scene.GetRootGameObjects();
                var objects = new List<object>();
                foreach (var obj in rootObjects)
                {
                    objects.Add(new { 
                        name = obj.name, 
                        address = GetAddress(obj),
                        hasChildren = obj.transform.childCount > 0
                    });
                }
                scenesData.Add(new { name = scene.name, path = scene.path, objects = objects, isLoaded = scene.isLoaded });
            }
            return scenesData;
        }

        public static void SelectResource(int index)
        {
            if (index >= 0 && index < FoundResources.Count)
            {
                SharedVariables["$target"] = FoundResources[index];
            }
        }

        public static InspectionResult Inspect(string expression)
        {
            var result = new InspectionResult { Path = expression };
            try
            {
                object obj = Evaluate(expression);
                if (obj == null)
                {
                    result.Value = "null";
                    result.Type = "null";
                    result.IsExpandable = false;
                    return result;
                }

                Type typeToInspect;
                object instanceToInspect;

                if (obj is Type t)
                {
                    typeToInspect = t;
                    instanceToInspect = null;
                    result.Type = t.Name;
                    result.Value = t.FullName;
                    result.Address = ""; 
                    result.IsExpandable = true;
                }
                else
                {
                    typeToInspect = obj.GetType();
                    instanceToInspect = obj;
                    result.Type = typeToInspect.Name;
                    result.Value = obj.ToString();
                    result.Address = GetAddress(obj);
                    result.IsExpandable = !IsPrimitive(typeToInspect);
                }
                
                if (result.IsExpandable)
                {
                    result.Members = GetMembers(typeToInspect, instanceToInspect);
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            return result;
        }

        private static string GetAddress(object obj)
        {
            if (obj == null) return "";
            try
            {
                // Try to get Pointer property (common in Il2CppInterop)
                var prop = obj.GetType().GetProperty("Pointer");
                if (prop != null && prop.PropertyType == typeof(IntPtr))
                {
                    IntPtr ptr = (IntPtr)prop.GetValue(obj);
                    return "0x" + ptr.ToString("X");
                }
            }
            catch { }
            return "";
        }

        private static bool IsPrimitive(Type t)
        {
            return t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t.IsEnum;
        }

        private static List<MemberInfoDTO> GetMembers(Type type, object instance)
        {
            var members = new List<MemberInfoDTO>();
            
            // 1. Collections
            if (instance != null && instance is IEnumerable enumerable && !(instance is string))
            {
                int index = 0;
                foreach (var item in enumerable)
                {
                    if (index > 100) break; // Limit
                    members.Add(new MemberInfoDTO
                    {
                        Name = $"[{index}]",
                        Type = item?.GetType().Name ?? "null",
                        Value = item?.ToString() ?? "null",
                        Address = GetAddress(item),
                        IsExpandable = item != null && !IsPrimitive(item.GetType())
                    });
                    index++;
                }
            }

            BindingFlags flags = BindingFlags.Public;
            if (instance == null) flags |= BindingFlags.Static;
            else flags |= BindingFlags.Instance;

            // 2. Properties
            foreach (var prop in type.GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length > 0) continue; // Skip indexers
                try
                {
                    object val = prop.GetValue(instance);
                    members.Add(new MemberInfoDTO
                    {
                        Name = prop.Name,
                        Type = prop.PropertyType.Name,
                        Value = val?.ToString() ?? "null",
                        Address = GetAddress(val),
                        IsExpandable = val != null && !IsPrimitive(val.GetType())
                    });
                }
                catch (Exception e)
                {
                    members.Add(new MemberInfoDTO { Name = prop.Name, Value = $"<Error: {e.Message}>" });
                }
            }

            // 3. Fields
            foreach (var field in type.GetFields(flags))
            {
                try
                {
                    object val = field.GetValue(instance);
                    members.Add(new MemberInfoDTO
                    {
                        Name = field.Name,
                        Type = field.FieldType.Name,
                        Value = val?.ToString() ?? "null",
                        Address = GetAddress(val),
                        IsExpandable = val != null && !IsPrimitive(val.GetType())
                    });
                }
                catch (Exception e)
                {
                    members.Add(new MemberInfoDTO { Name = field.Name, Value = $"<Error: {e.Message}>" });
                }
            }

            return members;
        }

        public static object Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;
            try
            {
                var parser = new CustomExpressionParser(expression);
                return parser.Evaluate();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }

    public class CustomExpressionParser
    {
        private readonly string _text;
        private int _pos;

        private class AssignmentTarget
        {
            public object Instance;
            public MemberInfo Member;
            public object Collection;
            public int Index;
            public AssignmentTarget Parent;
        }
        private AssignmentTarget _lastTarget;

        public CustomExpressionParser(string text)
        {
            _text = text;
            _pos = 0;
        }

        public object Evaluate()
        {
            object current = ParseRoot();

            while (_pos < _text.Length)
            {
                SkipWhitespace();
                if (_pos >= _text.Length) break;

                char c = _text[_pos];
                if (c == '.')
                {
                    _pos++; // Consume '.'
                    string memberName = ParseIdentifier();
                    
                    SkipWhitespace();
                    if (_pos < _text.Length && _text[_pos] == '(')
                    {
                        var args = ParseArgumentList('(', ')');
                        current = InvokeMethodOnTarget(current, memberName, args);
                        _lastTarget = null; // Method call result is not assignable
                    }
                    else
                    {
                        var parent = _lastTarget;
                        current = AccessMember(current, memberName, parent);
                    }
                }
                else if (c == '[')
                {
                    var args = ParseArgumentList('[', ']');
                    var parent = _lastTarget;
                    current = AccessIndexer(current, args, parent);
                }
                else if (c == '!' && _pos + 1 < _text.Length && _text[_pos + 1] == '=')
                {
                    _pos += 2; // Consume '!='
                    string rhsExpr = _text.Substring(_pos).Trim();
                    object rhsValue = EvaluateOrLiteral(rhsExpr);
                    return !CompareValues(current, rhsValue);
                }
                else if (c == '=')
                {
                    if (_pos + 1 < _text.Length && _text[_pos + 1] == '=')
                    {
                        _pos += 2; // Consume '=='
                        string rhsExpr = _text.Substring(_pos).Trim();
                        object rhsValue = EvaluateOrLiteral(rhsExpr);
                        return CompareValues(current, rhsValue);
                    }

                    _pos++; // Consume '='
                    string assignExpr = _text.Substring(_pos);
                    var assignParser = new CustomExpressionParser(assignExpr);
                    object assignValue = assignParser.Evaluate();
                    
                    PerformAssignment(_lastTarget, assignValue);
                    return assignValue;
                }
                else
                {
                    break;
                }
            }

            return current;
        }

        private void PerformAssignment(AssignmentTarget target, object value)
        {
            if (target == null) throw new Exception("Invalid assignment target");
            
            if (target.Member is PropertyInfo prop)
            {
                object converted = ConvertArg(value, prop.PropertyType);
                prop.SetValue(target.Instance, converted);
            }
            else if (target.Member is FieldInfo field)
            {
                object converted = ConvertArg(value, field.FieldType);
                field.SetValue(target.Instance, converted);
            }
            else if (target.Collection != null)
            {
                if (target.Collection is Array arr)
                {
                     Type elemType = arr.GetType().GetElementType();
                     object converted = ConvertArg(value, elemType);
                     arr.SetValue(converted, target.Index);
                }
                else if (target.Collection is IList list)
                {
                     Type listType = list.GetType();
                     Type elemType = typeof(object);
                     if (listType.IsGenericType) {
                         elemType = listType.GetGenericArguments()[0];
                     }
                     object converted = ConvertArg(value, elemType);
                     list[target.Index] = converted;
                }
            }

            if (target.Parent != null && target.Instance != null && target.Instance.GetType().IsValueType)
            {
                PerformAssignment(target.Parent, target.Instance);
            }
        }

        private object ParseRoot()
        {
            SkipWhitespace();
            if (_pos >= _text.Length) return null;

            char c = _text[_pos];

            // 1. Literals
            if (c == '"') return ParseStringLiteral();
            if (char.IsDigit(c) || c == '-') return ParseNumberLiteral();
            
            // 2. Identifiers (could be Type, Static Class, or bool/null)
            string ident = ParseIdentifier();
            
            if (ident == "true") return true;
            if (ident == "false") return false;
            if (ident == "null") return null;

            if (ReflectionEvaluator.SharedVariables.ContainsKey(ident))
            {
                return ReflectionEvaluator.SharedVariables[ident];
            }

            // 3. Type Resolution
            List<string> parts = new List<string> { ident };
            
            // Look ahead to capture A.B.C
            while (_pos < _text.Length)
            {
                int savePos = _pos;
                SkipWhitespace();
                if (_pos < _text.Length && _text[_pos] == '.')
                {
                    _pos++; // Consume '.'
                    parts.Add(ParseIdentifier());
                }
                else
                {
                    _pos = savePos; // Backtrack if not dot
                    break;
                }
            }

            // Now try to find the longest prefix that is a Type
            for (int i = parts.Count; i > 0; i--)
            {
                string typeName = string.Join(".", parts.Take(i));
                Type type = FindType(typeName);
                if (type != null)
                {
                    object current = null; // Static context initially
                    Type currentType = type;
                    bool accessedMember = false;
                    _lastTarget = null;

                    // Process the remaining parts that were tentatively parsed as type name
                    for (int j = i; j < parts.Count; j++)
                    {
                        accessedMember = true;
                        string memberName = parts[j];
                        bool isLast = (j == parts.Count - 1);
                        
                        if (isLast)
                        {
                            SkipWhitespace();
                            if (_pos < _text.Length && _text[_pos] == '(')
                            {
                                var args = ParseArgumentList('(', ')');
                                current = InvokeMethodOnTarget(currentType, current, memberName, args);
                                if (current != null) currentType = current.GetType();
                                _lastTarget = null;
                                continue;
                            }
                        }
                        
                        var parent = _lastTarget;
                        current = AccessMember(currentType, current, memberName, parent);
                        if (current != null) currentType = current.GetType();
                    }
                    
                    return accessedMember ? current : type; 
                }
            }

            throw new Exception($"Could not resolve type or identifier '{ident}'");
        }

        private object[] ParseArgumentList(char open, char close)
        {
            _pos++; // Consume open char
            List<object> args = new List<object>();
            
            SkipWhitespace();
            if (_pos < _text.Length && _text[_pos] == close)
            {
                _pos++;
                return args.ToArray();
            }

            while (_pos < _text.Length)
            {
                string argExpr = ReadUntilDelimiter(new char[] { ',', close });
                var subParser = new CustomExpressionParser(argExpr);
                args.Add(subParser.Evaluate());

                SkipWhitespace();
                if (_pos < _text.Length && _text[_pos] == ',')
                {
                    _pos++; // Consume comma
                }
                else if (_pos < _text.Length && _text[_pos] == close)
                {
                    _pos++; // Consume close
                    break;
                }
            }
            return args.ToArray();
        }

        private string ReadUntilDelimiter(char[] delimiters)
        {
            int start = _pos;
            int depthParen = 0;
            int depthBracket = 0;
            bool inQuote = false;

            while (_pos < _text.Length)
            {
                char c = _text[_pos];
                
                if (inQuote)
                {
                    if (c == '"' && _text[_pos-1] != '\\') inQuote = false;
                }
                else
                {
                    if (depthParen == 0 && depthBracket == 0 && delimiters.Contains(c))
                    {
                        break;
                    }

                    if (c == '"') inQuote = true;
                    else if (c == '(') depthParen++;
                    else if (c == ')') depthParen--;
                    else if (c == '[') depthBracket++;
                    else if (c == ']') depthBracket--;
                }
                _pos++;
            }
            
            return _text.Substring(start, _pos - start);
        }

        private string ParseIdentifier()
        {
            SkipWhitespace();
            int start = _pos;
            if (_pos < _text.Length && (char.IsLetter(_text[_pos]) || _text[_pos] == '_' || _text[_pos] == '$'))
            {
                _pos++;
                while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_' || _text[_pos] == '$'))
                {
                    _pos++;
                }
                return _text.Substring(start, _pos - start);
            }
            throw new Exception($"Expected identifier at position {start}");
        }

        private string ParseStringLiteral()
        {
            _pos++; // Consume "
            int start = _pos;
            while (_pos < _text.Length && _text[_pos] != '"')
            {
                if (_text[_pos] == '\\') _pos++; // Skip escape
                _pos++;
            }
            string res = _text.Substring(start, _pos - start);
            _pos++; // Consume "
            return res;
        }

        private object ParseNumberLiteral()
        {
            int start = _pos;
            bool isHex = false;

            if (_text[_pos] == '0' && _pos + 1 < _text.Length && (_text[_pos + 1] == 'x' || _text[_pos + 1] == 'X'))
            {
                isHex = true;
                _pos += 2; 
                start = _pos;
                while (_pos < _text.Length && IsHexDigit(_text[_pos]))
                {
                    _pos++;
                }
            }
            else
            {
                if (_text[_pos] == '-') _pos++;
                while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
                {
                    _pos++;
                }
            }

            string numStr = _text.Substring(start, _pos - start);
            
            if (isHex)
            {
                if (string.IsNullOrEmpty(numStr)) return 0;
                if (ulong.TryParse(numStr, System.Globalization.NumberStyles.HexNumber, null, out ulong ul)) return ul;
                throw new Exception($"Invalid hex number: 0x{numStr}");
            }

            if (numStr.Contains(".")) return double.Parse(numStr);
            
            if (long.TryParse(numStr, out long l)) return l;
            if (ulong.TryParse(numStr, out ulong ul2)) return ul2;
            
            return double.Parse(numStr);
        }

        private bool IsHexDigit(char c)
        {
            return char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private void SkipWhitespace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
        }

        // --- Reflection Helpers ---

        private static Type FindType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name);
                if (type != null) return type;
            }
            return null;
        }

        private object AccessMember(object target, string memberName, AssignmentTarget parent = null)
        {
            if (target == null) throw new Exception($"Cannot access member '{memberName}' on null");
            
            Type type = target as Type;
            object instance = target;
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic;

            if (type != null)
            {
                flags |= BindingFlags.Static;
                instance = null;
                if (TryGetMember(type, null, memberName, flags, out object result, out MemberInfo info))
                {
                    _lastTarget = new AssignmentTarget { Instance = null, Member = info, Parent = parent };
                    return result;
                }

                flags = BindingFlags.Public | BindingFlags.Instance;
                instance = type;
                type = typeof(Type);
            }
            else
            {
                type = target.GetType();
                flags |= BindingFlags.Instance;
            }

            if (TryGetMember(type, instance, memberName, flags, out object res, out MemberInfo info2))
            {
                _lastTarget = new AssignmentTarget { Instance = instance, Member = info2, Parent = parent };
                return res;
            }

            throw new Exception($"Member '{memberName}' not found on type '{type.FullName}'");
        }
        
        private object AccessMember(Type type, object instance, string memberName, AssignmentTarget parent = null)
        {
             var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
             if (TryGetMember(type, instance, memberName, flags, out object res, out MemberInfo info))
             {
                 _lastTarget = new AssignmentTarget { Instance = instance, Member = info, Parent = parent };
                 return res;
             }
             throw new Exception($"Member '{memberName}' not found on type '{type.FullName}'");
        }

        private bool TryGetMember(Type type, object instance, string memberName, BindingFlags flags, out object result, out MemberInfo memberInfo)
        {
            memberInfo = null;
            var prop = type.GetProperty(memberName, flags);
            if (prop != null) 
            {
                result = prop.GetValue(instance);
                memberInfo = prop;
                return true;
            }

            var field = type.GetField(memberName, flags);
            if (field != null) 
            {
                result = field.GetValue(instance);
                memberInfo = field;
                return true;
            }
            
            result = null;
            return false;
        }

        private object AccessIndexer(object target, object[] args, AssignmentTarget parent = null)
        {
            if (target == null) throw new Exception("Cannot index null");
            
            if (target is Array arr && args.Length == 1 && args[0] is int idx)
            {
                _lastTarget = new AssignmentTarget { Collection = arr, Index = idx, Parent = parent };
                return arr.GetValue(idx);
            }
            
            if (target is IList list && args.Length == 1 && args[0] is int listIdx)
            {
                _lastTarget = new AssignmentTarget { Collection = list, Index = listIdx, Parent = parent };
                return list[listIdx];
            }

            Type type = target.GetType();
            PropertyInfo indexer = null;
            
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (prop.GetIndexParameters().Length == args.Length)
                {
                    indexer = prop;
                    if (prop.Name == "Item") break;
                }
            }

            if (indexer != null)
            {
                var parameters = indexer.GetIndexParameters();
                object[] convertedArgs = new object[args.Length];
                for(int i=0; i<args.Length; i++)
                {
                    convertedArgs[i] = ConvertArg(args[i], parameters[i].ParameterType);
                }
                // Indexer assignment is not fully supported via reflection in this simple parser for write-back yet, 
                // but we can at least return the value.
                // For write-back to work with indexers, we'd need to store the indexer PropertyInfo in AssignmentTarget.
                // Currently AssignmentTarget has Collection/Index for arrays/lists, but not generic indexers.
                // Let's stick to arrays/lists for now.
                return indexer.GetValue(target, convertedArgs);
            }

            throw new Exception($"No suitable indexer found on type '{type.Name}'");
        }

        private object InvokeMethodOnTarget(object target, string methodName, object[] args)
        {
            Type type = target as Type;
            object instance = target;
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic;

            if (type != null)
            {
                flags |= BindingFlags.Static;
                instance = null;
            }
            else
            {
                type = target.GetType();
                flags |= BindingFlags.Instance;
            }
            
            return InvokeMethodOnTarget(type, instance, methodName, args);
        }

        private object InvokeMethodOnTarget(Type type, object instance, string methodName, object[] args)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            
            var methods = type.GetMethods(flags).Where(m => m.Name == methodName && m.GetParameters().Length == args.Length).ToList();
            
            if (methods.Count == 0) throw new Exception($"Method '{methodName}' not found on type '{type.Name}' with {args.Length} arguments");
            
            MethodInfo method = methods.First(); 
            
            var parameters = method.GetParameters();
            object[] convertedArgs = new object[args.Length];
            for(int i=0; i<args.Length; i++)
            {
                convertedArgs[i] = ConvertArg(args[i], parameters[i].ParameterType);
            }
            
            return method.Invoke(instance, convertedArgs);
        }

        private object ConvertArg(object arg, Type targetType)
        {
            if (arg == null) return null;
            if (targetType.IsAssignableFrom(arg.GetType())) return arg;
            return Convert.ChangeType(arg, targetType);
        }

        /// <summary>
        /// Try to evaluate rhsExpr normally; if it fails (e.g. unresolved identifier like an enum name),
        /// return the raw string so CompareValues can match via ToString/Enum.TryParse.
        /// </summary>
        private static object EvaluateOrLiteral(string rhsExpr)
        {
            try
            {
                return new CustomExpressionParser(rhsExpr).Evaluate();
            }
            catch
            {
                return rhsExpr;
            }
        }

        private static bool CompareValues(object left, object right)
        {
            if (left == null && right == null) return true;
            if (left == null || right == null) return false;

            // Direct equality
            if (left.Equals(right)) return true;

            // Enum vs unresolved identifier (string from ParseRoot throwing → caught as string, or Type)
            // The RHS like "OnTalkWithCharacter" will be resolved as a Type or throw.
            // If left is enum, try parsing right's ToString() as the same enum type.
            if (left.GetType().IsEnum)
            {
                string rightStr = right is Type t ? t.Name : right.ToString();
                if (Enum.TryParse(left.GetType(), rightStr, out object parsed))
                    return left.Equals(parsed);
            }
            if (right.GetType().IsEnum)
            {
                string leftStr = left is Type t ? t.Name : left.ToString();
                if (Enum.TryParse(right.GetType(), leftStr, out object parsed))
                    return right.Equals(parsed);
            }

            // Fallback: string comparison
            return string.Equals(left.ToString(), right.ToString(), StringComparison.Ordinal);
        }
    }
}
