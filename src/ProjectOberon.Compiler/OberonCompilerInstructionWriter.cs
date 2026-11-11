namespace ProjectOberon.Compiler;

public interface IOberonCompilerInstructionWriter
{
    void EmitRegister(AluOperation operation, int destination, int left, int right, bool carry = false);
    void EmitImmediate(AluOperation operation, int destination, int left, int immediate);
    void EmitMoveImmediate(int destination, int value);
    void EmitMoveFromHigh(int destination);
    void EmitLoad(int destination, int addressRegister, int offset, bool byteValue = false);
    void EmitStore(int source, int addressRegister, int offset, bool byteValue = false);
    int EmitBranch(ConditionCode condition = ConditionCode.Always, bool link = false);
    int EmitBranch(ConditionCode condition, bool link, bool invert);
    int EmitBranchRegister(int register, ConditionCode condition = ConditionCode.Always, bool link = false);
    int EmitBranch(
        OberonCompilerLabel label,
        ConditionCode condition = ConditionCode.Always,
        bool link = false);
    int EmitBranch(
        OberonCompilerLabel label,
        ConditionCode condition,
        bool link,
        bool invert);
    void Bind(OberonCompilerLabel label);
    void EmitReturn();
    void EmitCall(OberonCompilerLabel target);
    void EmitProcedurePrologue(int frameSize);
    void EmitSaveParameter(int register, int offset);
    void EmitProcedureReturn(int frameSize);
}
