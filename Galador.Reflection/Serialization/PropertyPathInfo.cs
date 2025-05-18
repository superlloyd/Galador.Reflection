using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Galador.Reflection.Serialization;

/// <summary>
/// This is a debugging class, that hold the path of the currently reading item.
/// In case of 2 path resolving to the same item this will be displayed as a warning
/// (the warning only happen for collection now).
/// This can happen since the deserializer rather use exiting property / item (when they exits) 
/// than creating new ones. and also use public property and setter when possible.
/// </summary>
public class PropertyPathInfo
{
    List<string> _path = new List<string>();

    public void Push()
        => _path.Add(string.Empty);
    public void Pop()
        => _path.RemoveAt(_path.Count - 1);
    public void Set(string name)
        => _path[_path.Count - 1] = name;
    override public string ToString()
        => string.Join("", _path);
}
