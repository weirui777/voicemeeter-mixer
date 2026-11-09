import ctypes, sys, time

# 用法: python set_one.py "Strip[4].Gain" -1.12
# 注意：Voicemeeter Remote API 的写入要靠客户端调用 VBVMR_IsParametersDirty() 才会真正生效，
# 所以这里写完之后泵一小会儿再回读校验。
vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_GetParameterFloat.restype = ctypes.c_long
vm.VBVMR_IsParametersDirty.restype = ctypes.c_long

vm.VBVMR_Login()
name = sys.argv[1]
val = float(sys.argv[2])
rc = vm.VBVMR_SetParameterFloat(name.encode(), ctypes.c_float(val))
for _ in range(40):
    vm.VBVMR_IsParametersDirty()
    time.sleep(0.01)
v = ctypes.c_float()
rc2 = vm.VBVMR_GetParameterFloat(name.encode(), ctypes.byref(v))
print("set %-16s = %.3f -> rc=%d, readback=%.3f" % (name, val, rc, v.value if rc2 == 0 else float('nan')))
