import ctypes, sys

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_IsParametersDirty.restype = ctypes.c_long

vm.VBVMR_Login()
for name in sys.argv[1:]:
    v = ctypes.c_float()
    rc = vm.VBVMR_GetParameterFloat(name.encode(), ctypes.byref(v))
    print("%-16s = %s" % (name, ("%.3f" % v.value) if rc == 0 else "ERR"))
vm.VBVMR_Logout()
