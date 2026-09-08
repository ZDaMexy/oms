// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using osu.Game.Skinning.Gameplay.Scripting;

if (args.Length is < 2 or > 3 || args[0] is not ("check" or "compile" or "verify"))
{
    Console.Error.WriteLine("check SOURCE | compile SOURCE OUTPUT | verify BYTECODE");
    return 2;
}

try
{
    int maximum = args[0] == "verify" ? GameplaySkinScriptProgram.MAX_BYTECODE_BYTES : GameplaySkinScriptProgram.MAX_SOURCE_BYTES;
    using var input = File.OpenRead(args[1]);
    if (input.Length > maximum)
        throw new GameplaySkinScriptException(GameplaySkinScriptFaultCode.SourceLimit);
    byte[] bytes = new byte[(int)input.Length];
    input.ReadExactly(bytes);
    GameplaySkinScriptProgram program = args[0] == "verify" ? GameplaySkinScriptCompiler.DecodeBytecode(bytes) : GameplaySkinScriptCompiler.Compile(bytes);
    if (args[0] == "compile")
    {
        if (args.Length != 3)
            return 2;
        File.WriteAllBytes(args[2], GameplaySkinScriptCompiler.EncodeBytecode(program));
    }
    Console.WriteLine($"OMS script v{program.SourceVersion}/bytecode v{program.BytecodeVersion}/compiler v{program.CompilerVersion}/runtime v{program.RuntimeVersion}: "
                      + $"{program.InstructionCount} instructions, {program.RegisterCount} state cells, {program.HeapCells} heap cells, {program.Targets.Count} targets.");
    Console.WriteLine($"Fingerprint: {program.Fingerprint}");
    return 0;
}
catch (GameplaySkinScriptException error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine("OMS-SKIN-SCRIPT-TOOL-IO");
    return 1;
}
