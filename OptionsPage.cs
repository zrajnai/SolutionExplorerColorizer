using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace SolutionExplorerProjectStyler
{
    [Guid("4a2d8e12-3f11-4b99-88ae-ef1234567890")]
    public class OptionsPage : DialogPage
    {
        [Category("Project Styler")]
        [DisplayName("Unloaded Project Color")]
        [Description("Color applied to unloaded projects in the Solution Explorer.")]
        public Color UnloadedProjectColor { get; set; } = Color.Gray;

        [Category("Project Styler")]
        [DisplayName("Missing Project Color")]
        [Description("Color applied to missing ('not found') projects in the Solution Explorer.")]
        public Color MissingProjectColor { get; set; } = Color.IndianRed;
    }
}