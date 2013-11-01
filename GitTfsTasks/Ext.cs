using Microsoft.Build.Utilities;

namespace GitTfsTasks
{
    static class Ext
    {
        public static void MaybeSetMetadata(this TaskItem item, string name, string value)
        {
            if(value != null)
                item.SetMetadata(name, value);
        }
    }
}
