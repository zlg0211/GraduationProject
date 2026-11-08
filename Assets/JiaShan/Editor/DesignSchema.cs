using System;

namespace JiaShan.EditorTools
{
    [Serializable] public sealed class DesignDocument { public DesignPage[] pages; }
    [Serializable] public sealed class DesignPage { public int id; public string name; public DesignNode[] nodes; public DesignControl[] controls; public bool nav; }
    [Serializable] public sealed class DesignPoint { public float x; public float y; }
    [Serializable] public sealed class DesignNode
    {
        public string kind, text, fill, stroke, file, align;
        public float x, y, w, h, r, size, sw, opacity;
        public bool bold, closed, crop;
        public DesignPoint[] points;
    }
    [Serializable] public sealed class DesignControl { public string kind, text, placeholder; public float x, y, w, h; public bool password; }
}
