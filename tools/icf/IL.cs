using System.Reflection;
using System.Reflection.Emit;

/// <summary>逐条读取 IL 指令，只解析操作数长度和 4/8 字节常量与元数据令牌。</summary>
static class IL
{
    static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null))
        .ToDictionary(c => c.Value);

    public readonly record struct Instruction(OpCode Code, long Operand);

    public static IEnumerable<Instruction> Read(byte[] il)
    {
        int i = 0;
        while (i < il.Length)
        {
            short value = il[i] == 0xFE ? (short)(0xFE00 | il[i + 1]) : il[i];
            i += il[i] == 0xFE ? 2 : 1;
            var code = Codes[value];
            long operand = 0;
            switch (code.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    operand = (sbyte)il[i];
                    i += 1;
                    break;
                case OperandType.InlineVar:
                    operand = BitConverter.ToInt16(il, i);
                    i += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    operand = BitConverter.ToInt64(il, i);
                    i += 8;
                    break;
                case OperandType.InlineSwitch:
                    i += 4 + 4 * BitConverter.ToInt32(il, i);
                    break;
                default:
                    operand = BitConverter.ToInt32(il, i);
                    i += 4;
                    break;
            }
            yield return new Instruction(code, operand);
        }
    }
}
