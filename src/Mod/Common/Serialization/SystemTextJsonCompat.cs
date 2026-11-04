using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ---------------------------------------------------------------------------
// 只在 FluxForge 程序集内使用的 System.Text.Json 兼容层。
//
// 迁移到官方 Mono 框架后目标框架固定为 netstandard2.1，而 System.Text.Json
// 不是 netstandard2.1 的一部分；游戏自身只带 Newtonsoft.Json。为了同时满足
// “不新增第三方包”“不改 src/Presentation” 两个约束，这里用游戏自带的
// Newtonsoft.Json 实现 mod 实际用到的那一小部分 STJ API。
//
// 覆盖范围（全部来自当前源码）：
//   JsonDocument.Parse(string/byte[]) + RootElement + Dispose
//   JsonElement.ValueKind / TryGetProperty / EnumerateObject / GetArrayLength
//                / 索引器 / TryGetInt32 / GetString / Deserialize<T>()
//   JsonProperty、JsonValueKind、JsonException
//   JsonSerializer.Deserialize<T>(string)
//
// 这是编译期/运行期的适配层，不冒充完整 STJ：新增 STJ 用法时需要同步扩展。
// ---------------------------------------------------------------------------
namespace System.Text.Json
{
    public enum JsonValueKind
    {
        Undefined = 0,
        Object,
        Array,
        String,
        Number,
        True,
        False,
        Null,
    }

    public sealed class JsonException : Exception
    {
        public JsonException(string message)
            : base(message)
        {
        }

        public JsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class JsonDocument : IDisposable
    {
        private readonly JToken _root;

        private JsonDocument(JToken root)
        {
            _root = root;
        }

        public JsonElement RootElement
        {
            get { return new JsonElement(_root); }
        }

        public static JsonDocument Parse(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            string text = json.TrimStart('\uFEFF');
            try
            {
                return new JsonDocument(JToken.Parse(text));
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                throw new JsonException("不是合法 JSON：" + e.Message, e);
            }
        }

        public static JsonDocument Parse(byte[] utf8Json)
        {
            if (utf8Json == null)
            {
                throw new ArgumentNullException(nameof(utf8Json));
            }

            return Parse(Encoding.UTF8.GetString(utf8Json));
        }

        public void Dispose()
        {
            // JToken 没有需要释放的非托管资源。
        }
    }

    public readonly struct JsonElement
    {
        public readonly JToken Token;

        public JsonElement(JToken token)
        {
            Token = token;
        }

        public JsonValueKind ValueKind
        {
            get
            {
                if (Token == null)
                {
                    return JsonValueKind.Undefined;
                }

                switch (Token.Type)
                {
                    case JTokenType.Object:
                        return JsonValueKind.Object;
                    case JTokenType.Array:
                        return JsonValueKind.Array;
                    case JTokenType.String:
                        return JsonValueKind.String;
                    case JTokenType.Integer:
                    case JTokenType.Float:
                        return JsonValueKind.Number;
                    case JTokenType.Boolean:
                        return Token.Value<bool>() ? JsonValueKind.True : JsonValueKind.False;
                    case JTokenType.Null:
                        return JsonValueKind.Null;
                    default:
                        return JsonValueKind.Undefined;
                }
            }
        }

        public bool TryGetProperty(string propertyName, out JsonElement value)
        {
            if (Token is JObject obj && obj.TryGetValue(propertyName, out JToken found))
            {
                value = new JsonElement(found);
                return true;
            }

            value = default(JsonElement);
            return false;
        }

        public IEnumerable<JsonProperty> EnumerateObject()
        {
            if (!(Token is JObject obj))
            {
                throw new InvalidOperationException("JSON 节点不是对象，不能枚举属性。");
            }

            foreach (JProperty property in obj.Properties())
            {
                yield return new JsonProperty(property.Name, new JsonElement(property.Value));
            }
        }

        public IEnumerable<JsonElement> EnumerateArray()
        {
            if (!(Token is JArray array))
            {
                throw new InvalidOperationException("JSON 节点不是数组。");
            }

            foreach (JToken item in array)
            {
                yield return new JsonElement(item);
            }
        }

        public int GetArrayLength()
        {
            if (!(Token is JArray array))
            {
                throw new InvalidOperationException("JSON 节点不是数组。");
            }

            return array.Count;
        }

        public JsonElement this[int index]
        {
            get
            {
                if (!(Token is JArray array))
                {
                    throw new InvalidOperationException("JSON 节点不是数组。");
                }

                return new JsonElement(array[index]);
            }
        }

        public bool TryGetInt32(out int value)
        {
            if (Token != null && Token.Type == JTokenType.Integer)
            {
                try
                {
                    long parsed = Token.Value<long>();
                    if (parsed >= int.MinValue && parsed <= int.MaxValue)
                    {
                        value = (int)parsed;
                        return true;
                    }
                }
                catch (Exception)
                {
                    // 超出 Int64 的大整数按“不是 int”处理。
                }
            }

            value = 0;
            return false;
        }

        public string GetString()
        {
            if (Token == null || Token.Type == JTokenType.Null)
            {
                return null;
            }

            if (Token.Type != JTokenType.String)
            {
                throw new InvalidOperationException("JSON 节点不是字符串。");
            }

            return Token.Value<string>();
        }

        public T Deserialize<T>()
        {
            return JsonSerializer.DeserializeToken<T>(Token);
        }
    }

    public readonly struct JsonProperty
    {
        public readonly string Name;
        public readonly JsonElement Value;

        public JsonProperty(string name, JsonElement value)
        {
            Name = name;
            Value = value;
        }
    }

    public static class JsonSerializer
    {
        public static T Deserialize<T>(string json)
        {
            if (json == null)
            {
                return default(T);
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                throw new JsonException("反序列化失败：" + e.Message, e);
            }
        }

        public static T DeserializeToken<T>(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return default(T);
            }

            try
            {
                return token.ToObject<T>();
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                throw new JsonException("反序列化失败：" + e.Message, e);
            }
        }
    }
}
