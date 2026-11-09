import ctypes, time

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_IsParametersDirty.restype = ctypes.c_long

ORIG = {
    "Bus[0].Gain": -9.8, "Bus[1].Gain": -3.19, "Bus[2].Gain": -4.51,
    "Bus[3].Gain": -60.0, "Bus[4].Gain": -60.0, "Bus[5].Gain": 0.0,
    "Bus[6].Gain": 0.0, "Bus[7].Gain": 0.0,
    "Strip[0].Gain": -60.0, "Strip[1].Gain": -60.0, "Strip[2].Gain": -60.0,
    "Strip[3].Gain": -3.49, "Strip[4].Gain": -2.32, "Strip[5].Gain": 0.0,
    "Strip[6].Gain": 0.0, "Strip[7].Gain": 0.0,
}


def getf(n):
    v = ctypes.c_float()
    rc = vm.VBVMR_GetParameterFloat(n.encode(), ctypes.byref(v))
    return v.value if rc == 0 else None


vm.VBVMR_Login()
print("before:")
for n in ORIG:
    print("   %-15s %s" % (n, getf(n)))

# 把所有增益槽一次性写回原值，再泵一次（泵会提交整个缓冲）
for n, v in ORIG.items():
    vm.VBVMR_SetParameterFloat(n.encode(), ctypes.c_float(v))
for i in range(30):
    vm.VBVMR_IsParametersDirty()
    time.sleep(0.01)

print("after:")
ok = True
for n, want in ORIG.items():
    cur = getf(n)
    good = cur is not None and abs(cur - want) < 0.05
    ok = ok and good
    print("   %-15s want=%8.2f now=%8s %s" % (n, want, cur, "OK" if good else "!!"))
print("ALL OK" if ok else "MISMATCH")
vm.VBVMR_Logout()
