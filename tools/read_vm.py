import ctypes, sys, io, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
L = ctypes.c_long
vm.VBVMR_Login.restype = L
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_GetParameterFloat.restype = L
print("login", vm.VBVMR_Login())


def getf(name):
    v = ctypes.c_float(0)
    vm.VBVMR_GetParameterFloat(name.encode(), ctypes.byref(v))
    return round(v.value, 3)


print("Bus[0].Gain (A1) =", getf("Bus[0].Gain"))
print("Strip[3].Gain (虚拟输入) =", getf("Strip[3].Gain"))
print("Strip[4].Gain (虚拟AUX) =", getf("Strip[4].Gain"))
vm.VBVMR_Logout()
