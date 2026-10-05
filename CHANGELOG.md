# Değişiklik Günlüğü

Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/), sürümleme [Semantic Versioning](https://semver.org/lang/tr/) esaslıdır.

## [0.1.3] - 2026-10-05

### Değişti
- "Ekibim" sekmesi herkese açık; bağlı personeli olmayan kullanıcı açıklayıcı bir mesaj görür. Kapsam yine sunucuda belirlenir.

### Eklendi
- Arka plan iş parçacığındaki yakalanmamış hatalar da hata günlüğüne yazılır.
- Demo modu gerçek kurulumdan bağımsız çalışır (ayrı tek örnek kilidi, genel ad ve simge).

## [0.1.2] - 2026-10-05

### Eklendi
- Uygulama adı yapılandırmadan (`Marka:UygulamaAdi`) okunur.
- Logo, portalın marka ayarından çalışma anında alınır ve yerelde önbelleğe yazılır.
- Yayın betiği firma logosundan uygulama ikonu üretir; repoya firma bilgisi girmez.

## [0.1.1] - 2026-10-05

### Eklendi
- Giriş ekranında sürüm numarası.

## [0.1.0] - 2026-10-05

### Eklendi
- Giriş, mola, moladan dönüş ve çıkış kaydı; sistem tepsisi ve Windows açılışında başlama.
- Klavye/fare hareketsizliğinden otomatik mola (15 dk) ve çıkış (120 dk); eşikler sunucudan.
- Moladan dönüş sorusu ve "mesaiye başla" hatırlatması.
- "Ekibim": yöneticinin bağlı personeli anlık durum ve günlük geçmişle görmesi.
- Velopack ile delta otomatik güncelleme ve personeli bölmeden kurulum.
- `--demo` modu.
