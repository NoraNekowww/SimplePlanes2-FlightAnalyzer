using System;
using System.Reflection;

class Program
{
    static void Main()
    {
        string managed = @"E:\SteamLibrary\steamapps\common\SimplePlanes 2\SimplePlanes 2_Data\Managed";
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string shortName = e.Name.Split(new char[] { ',' })[0].Trim() + ".dll";
            string path = System.IO.Path.Combine(managed, shortName);
            if (System.IO.File.Exists(path)) return Assembly.LoadFrom(path);
            return null;
        };

        Assembly gameAsm = Assembly.LoadFrom(System.IO.Path.Combine(managed, "Game.dll"));
        Type airType = gameAsm.GetType("Assets.Scripts.Craft.AircraftScript");

        DumpMethod(airType, "UpdateCenterOfMass");
        DumpMethod(airType, "UpdateAerodynamicCenter");
        
        // 查找所有包含 DrawLine 或 VectorLine 的方法
        foreach (var t in gameAsm.GetTypes())
        {
            if (t.Name.Contains("Gizmo") || t.Name.Contains("Trail") || t.Name.Contains("Line"))
            {
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (m.Name.Contains("Draw") || m.Name.Contains("Render") || m.Name.Contains("Create"))
                    {
                        var mb = m.GetMethodBody();
                        if (mb != null && mb.GetILAsByteArray().Length > 20)
                        {
                            Console.WriteLine("Candidate: " + t.FullName + "." + m.Name);
                        }
                    }
                }
            }
        }
    }

    static void DumpMethod(Type t, string methodName)
    {
        if (t == null) return;
        MethodInfo m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        if (m == null)
        {
            Console.WriteLine("Method not found: " + methodName);
            return;
        }
        Console.WriteLine("\n--- " + t.Name + "." + methodName + " ---");
        MethodBody mb = m.GetMethodBody();
        if (mb == null) return;
        Module mod = m.Module;
        byte[] il = mb.GetILAsByteArray();
        for (int i = 0; i < il.Length - 4; i++)
        {
            byte op = il[i];
            if (op == 0x28 || op == 0x6F || op == 0x7B || op == 0x7D)
            {
                int token = BitConverter.ToInt32(il, i + 1);
                try
                {
                    var mi = mod.ResolveMember(token);
                    if (mi.DeclaringType.Name.Contains("Line") || mi.DeclaringType.Name.Contains("Draw") || mi.DeclaringType.Name.Contains("Render") || mi.DeclaringType.Name.Contains("Vector") || mi.DeclaringType.Name.Contains("Shape"))
                        Console.WriteLine("  [" + i + "] " + mi.DeclaringType.Name + "." + mi.Name);
                }
                catch {}
            }
        }
    }
}
