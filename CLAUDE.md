# Kalıp Takip — oturum kuralları
- Her değişiklikte: `YOL_HARITASI.md` (yeni adım satırı + gerekirse açık konular), `OKU_BENI.txt` (ayrıntı), sürüm damgası (`Mobil/index.html` içindeki `SURUM`) güncellenir.
- Commit sonrası sürüm etiketi: `git tag surum-<ad> <commit> && git push --tags`.
- Eskiyi bozmadan üzerine ekle. Veritabanı (.db) asla GitHub'a konmaz. Depo özel kalır.
- Push sonrası GitHub Actions derlemesinin yeşil bittiğini kontrol et (`gh run list -L 1`); VB'de büyük/küçük harf farkı olmadan aynı adlı değişken/özellik çakışmasına dikkat.
- Kullanıcı Türkçe konuşur; yanıtlar Türkçe, sade ve adım adım.
