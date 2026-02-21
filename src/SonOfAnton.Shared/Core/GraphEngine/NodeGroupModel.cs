using System;
using System.Collections.Generic;

namespace SonOfAnton.Shared.Core.GraphEngine;

public class NodeGroupModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Yeni Grup";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Color { get; set; } = "#313244";
    public List<Guid> NodeIds { get; set; } = new();
}
