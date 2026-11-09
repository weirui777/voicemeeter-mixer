import ctypes, time

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_Logout.restype = ctypes.c_long
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_IsParametersDirty.restype = ctypes.c_long

ORIG = -3.19


def getf(n):
    v = ctypes.c_float()
    rc = vm.VBVMR_GetParameterFloat(n.encode(), ctypes.byref(v))
    return ("%.3f" % v.value) if rc == 0 else ("ERR%d" % rc)


def setpump(n, val, pumps=25):
    rc = vm.VBVMR_SetParameterFloat(n.encode(), ctypes.c_float(val))
    for _ in range(pumps):
        vm.VBVMR_IsParametersDirty()
        time.sleep(0.01)
    return rc, getf(n)


print("1) login rc=", vm.VBVMR_Login())
print("   write -21 ->", setpump("Bus[1].Gain", -21.0))

print("2) foreign client: login + read + logout")
vm.VBVMR_Login()
print("   foreign read ->", getf("Bus[1].Gain"))
vm.VBVMR_Logout()
time.sleep(0.3)

print("3) our session writes -22 (same session, after foreign logout)")
print("   write -22 ->", setpump("Bus[1].Gain", -22.0))

print("4) our session re-login, then write -23")
vm.VBVMR_Login()
time.sleep(0.2)
print("   write -23 ->", setpump("Bus[1].Gain", -23.0))

print("5) foreign logout ONLY (no foreign login)")
vm.VBVMR_Logout()
time.sleep(0.3)
vm.VBVMR_Login()
time.sleep(0.2)
print("   write -24 ->", setpump("Bus[1].Gain", -24.0))

print("6) restore ->", setpump("Bus[1].Gain", ORIG))
vm.VBVMR_Logout()
