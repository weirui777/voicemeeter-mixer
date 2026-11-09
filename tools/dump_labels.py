import ctypes

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_GetParameterStringA.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
vm.VBVMR_Login()

for pfx in ("Strip", "Bus"):
    for i in range(6):
        b = ctypes.create_string_buffer(128)
        vm.VBVMR_GetParameterStringA(("%s[%d].Label" % (pfx, i)).encode(), b)
        raw = b.value
        shown = None
        for enc in ("gbk", "utf-8", "latin-1"):
            try:
                shown = raw.decode(enc)
                print("%s[%d] = %s   [%s]" % (pfx, i, shown, enc))
                break
            except Exception:
                continue
        if shown is None:
            print("%s[%d] = <undecodable> %r" % (pfx, i, raw))
