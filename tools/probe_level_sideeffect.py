import ctypes, time

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_GetLevel.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_IsParametersDirty.restype = ctypes.c_long


def getf(n):
    v = ctypes.c_float()
    rc = vm.VBVMR_GetParameterFloat(n.encode(), ctypes.byref(v))
    return v.value if rc == 0 else None


def level(mode, ch):
    v = ctypes.c_float()
    rc = vm.VBVMR_GetLevel(mode, ch, ctypes.byref(v))
    return v.value if rc == 0 else None


vm.VBVMR_Login()
print("1) baseline")
for n in ("Bus[0].Gain", "Bus[1].Gain", "Strip[3].Gain", "Strip[4].Gain"):
    print("   %-14s %s" % (n, getf(n)))

print("2) call GetLevel(3,0) ->", level(3, 0))
for n in ("Bus[0].Gain", "Bus[1].Gain", "Strip[3].Gain", "Strip[4].Gain"):
    print("   %-14s %s" % (n, getf(n)))

print("3) call GetLevel(1,12) ->", level(1, 12))
for n in ("Bus[0].Gain", "Bus[1].Gain", "Strip[3].Gain", "Strip[4].Gain"):
    print("   %-14s %s" % (n, getf(n)))

print("4) now pump once and re-read (does the level leak into the server?)")
vm.VBVMR_IsParametersDirty()
time.sleep(0.05)
for n in ("Bus[0].Gain", "Bus[1].Gain", "Strip[3].Gain", "Strip[4].Gain"):
    print("   %-14s %s" % (n, getf(n)))

print("5) ground truth from another session")
vm.VBVMR_Logout()
time.sleep(0.2)
vm.VBVMR_Login()
for n in ("Bus[0].Gain", "Bus[1].Gain", "Strip[3].Gain", "Strip[4].Gain"):
    print("   %-14s %s" % (n, getf(n)))
vm.VBVMR_Logout()
