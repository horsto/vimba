using System.Collections.Generic;
using System.ComponentModel;
using VmbNET;

namespace Bonsai.VimbaX
{
    class SerialNumberConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            // Ensure the bundled native VmbC.dll is discoverable so the dropdown
            // works without the Vimba X SDK being on PATH.
            VmbNativeLoader.EnsureNativeSearchPath();
            using var vmbSystem = IVmbSystem.Startup();
            var cameraList = vmbSystem.GetCameras();
            var values = new List<string>(cameraList.Count);
            for (int i = 0; i < cameraList.Count; i++)
            {
                var serialNumber = cameraList[i].Serial;
                if (!string.IsNullOrEmpty(serialNumber))
                {
                    values.Add(serialNumber);
                }
            }

            return new StandardValuesCollection(values);
        }
    }
}
