import ctypes, time

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_Logout.restype = ctypes.c_long
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_IsParametersDirty.restype = ctypes.c_long


def getf(n):
    v = ctypes.c_float()
    rc = vm.VBVMR_GetParameterFloat(n.encode(), ctypes.byref(v))
    return ("%.3f" % v.value) if rc == 0 else ("ERR%d" % rc)


def setpump(n, val, pumps=30):
    vm.VBVMR_SetParameterFloat(n.encode(), ctypes.c_float(val))
    for _ in range(pumps):
        vm.VBVMR_IsParametersDirty()
        time.sleep(0.01)
    return getf(n)


print("A. login rc=", vm.VBVMR_Login())
print("   write Bus[1].Gain=-21 ->", setpump("Bus[1].Gain", -21.0))

print("B. logout")
print("   logout rc=", vm.VBVMR_Logout())
time.sleep(0.3)
print("   read without login  ->", getf("Bus[1].Gain"))

print("C. login again rc=", vm.VBVMR_Login())
print("   write Bus[1].Gain=-22 ->", setpump("Bus[1].Gain", -22.0))
print("   write Bus[2].Gain=-23 ->", setpump("Bus[2].Gain", -23.0))

print("D. simulate foreign client: logout, then read+login only")
vm.VBVMR_Logout()
time.sleep(0.2)
print("   login rc=", vm.VBVMR_Login())
print("   read  ->", getf("Bus[1].Gain"))
print("   write Bus[1].Gain=-24 ->", setpump("Bus[1].Gain", -24.0))

vm.VBVMR_Logout()
