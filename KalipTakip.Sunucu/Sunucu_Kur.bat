@echo off
REM ARTIK GEREKLI DEGIL. KalipTakipSunucu.exe yonetici izni olmadan dogrudan acilir.
REM Sadece Windows guvenlik duvari telefonlari engelliyorsa (yonetici ile) bir kez calistirin.
netsh advfirewall firewall add rule name="KalipTakip Mobil" dir=in action=allow protocol=TCP localport=8080
pause
