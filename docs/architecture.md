# Canlı Araç Takip Platformu: Mimari ve Veri Akışı

> Akışların adım adım çizimleri. Genel bakış, gözlemler ve ölçümler için [README](../README.md)'ye bakın.
> Diyagramlar Mermaid ile yazıldı; GitHub'da ve VS Code Markdown önizlemesinde çizim olarak görünür.
> Adlar koddaki sınıf adlarıdır, böylece bir kutudan doğrudan dosyaya gidilebilir.

## 1. Tek cümleyle

Backend, Entur'un araç konum akışını 3 saniyede bir okur. Yeni konumları önce diske, oradan veritabanına yazar; bellekteki canlı durumu günceller ve her tarayıcıya yalnızca abone olduğu alandaki ya da hatlardaki değişiklikleri WebSocket ile gönderir. Uyarı ve sefer akışları 30 saniyede bir okunup bellekte tutulur. Hat ve durak adları ile güzergahlar Journey Planner'dan gelir. Arayüz giriş ister; canlı veriyi haritada gösterir, geçmişi, uyarıları, gecikmeyi ve sistem durumunu REST ile sorar.

## 2. Büyük resim

```mermaid
flowchart TB
    subgraph Entur["Entur (dış kaynaklar)"]
        direction LR
        EnturVP["GTFS-RT vehicle-positions<br/>3 sn"]
        EnturX["GTFS-RT alerts · trip-updates<br/>30 sn"]
        JP["Journey Planner<br/>adlar · güzergahlar · planlanan saatler"]
    end

    subgraph Backend["Backend (ASP.NET Core 8)"]
        direction TB
        subgraph CanliYol["Canlı yol (bellekte)"]
            direction LR
            Poll["FeedPollingWorker<br/>→ IngestionService"] --> Norm["SnapshotNormalizer"] --> Live["InMemoryLiveState<br/>3 sa hafıza · 10 dk canlı"] --> Hub["VehicleSocketHub<br/>ClientOutbox + abonelik"]
        end
        subgraph KaliciYol["Kalıcı yol (disk → veritabanı)"]
            direction LR
            Spool["FilePositionSpool"] --> Writer["PositionWriterWorker"]
            Ret["RetentionWorker"]
        end
        subgraph Yan["Yan akışlar ve REST"]
            direction LR
            Side["AlertService<br/>TripUpdateService"]
            Ref["ReferenceDataService<br/>TripRouteService"]
            Rest["REST API<br/>JWT, admin rolü"]
        end
    end

    DB[("PostgreSQL + TimescaleDB<br/>vehicle_positions · users · refresh_tokens · transit_*")]

    subgraph Frontend["Frontend (Next.js 16)"]
        direction LR
        Session["session-store<br/>access token bellekte"]
        Sock["VehicleSocket"] --> Store["LiveVehicleStore<br/>React dışında"] --> Map["MapLibre"]
        Query["TanStack Query"]
    end

    EnturVP -->|protobuf| Poll
    Live -->|yeni konumlar| Spool
    Writer --> DB
    Ret -->|drop_chunks| DB
    EnturX --> Side
    Side -. "alerts version" .-> Hub
    JP --> Ref
    Ref --> DB
    Rest --> Side
    Rest --> Ref
    Rest --> DB
    Hub -->|"WebSocket + access_token"| Sock
    Query -->|"HTTP + Bearer"| Rest
    Session -.-> Sock
    Session -.-> Query
```

Verinin iki ayrı yolu var ve birbirlerini beklemezler:

- **Canlı yol:** normalizasyon → canlı durum → istemcilerin giden kutusu. Tamamen bellekte; veritabanına dokunmaz.
- **Kalıcı yol:** yeni konumlar önce diske (`FilePositionSpool`), oradan veritabanına yazılır.

Bu ayrım sayesinde veritabanı durduğunda harita canlı kalır, bir istemci yavaşladığında da yazma etkilenmez.

## 3. Bileşenler ve sorumlulukları

| Bileşen | Tek işi | Katman |
|---|---|---|
| `FeedPollingWorker` | Araç akışını 3 sn'de bir okutur; bir döngü hata verse de döngü sürer | Infrastructure |
| `EnturFeedSource` | HTTP + protobuf. Süre sınırı yanıt gövdesini de kapsar (yarıda donan bağlantı takılmaz) | Infrastructure |
| `IngestionService` | Bir okuma döngüsü: oku → ayıkla → canlı duruma uygula → diske yaz → yayınla | Application |
| `SnapshotNormalizer` | Araç başına en yeni konumu seçer; kimliksiz, zamansız, koordinatı geçersiz ve gelecekten gelen kayıtları eler | Application |
| `InMemoryLiveState` | 3 saatlik pencerede araç başına son konumu **hatırlar** (tekrar kaydetmemek için); son 10 dakikada bildirenleri **canlı** sayar | Application arayüzü, Infrastructure uygulaması |
| `FilePositionSpool` | Veritabanına yazılmamış partileri dosyada tutar (`.tmp` yaz + atomik yeniden adlandırma) | Infrastructure |
| `PositionWriterWorker` | Spool'u sırayla veritabanına yazar; hata olursa 1 → 15 sn aralıklarla yeniden dener | Infrastructure |
| `RetentionWorker` | Saklama süresini aşan chunk'ları düşürür | Infrastructure |
| `DatabaseInitializer` | Açılışta migration'ları ve chunk aralığını uygular, canlı durumu veritabanından geri yükler | Infrastructure |
| `VehicleSocketHub` / `VehicleSocketSession` | Değişiklikleri istemcilere dağıtır; her istemcinin kendi giden kutusu, gönderim döngüsü ve aboneliği var | Api |
| `AlertService`, `TripUpdateService` | Uyarı ve sefer akışlarını bellekte değişmez bir liste olarak tutar; okuyucular kilit almaz | Application |
| `ReferenceDataService`, `TripRouteService` | Hat/durak adlarını toplar ve saklar; sefer güzergahlarını önbellekler (Journey Planner) | Application |
| `AuthService` | Kayıt, giriş, refresh rotation ve çalınma tespiti, çıkış | Application |
| REST API | Araçlar, geçmiş, uyarılar, sefer güzergahı ve gecikmesi, sistem durumu, auth | Api |

Katman bağımlılığı tek yönlüdür: `Api → Infrastructure → Application → Domain`. Kural mimari testleriyle zorlanır; Application ne Infrastructure'ı ne ASP.NET'i ne de JWT kütüphanelerini tanır.

## 4. Akış 1: Bir okuma döngüsü

**a) Her 3 saniyede: oku, ayıkla, uygula, diske yaz, yayınla**

```mermaid
sequenceDiagram
    autonumber
    participant I as IngestionService<br/>(FeedPollingWorker çağırır)
    participant E as Entur
    participant L as InMemoryLiveState
    participant S as FilePositionSpool
    participant H as VehicleSocketHub

    I->>E: GET vehicle-positions
    E-->>I: FeedMessage (~1,7 MB, ~8.400 entity)
    Note over I: SnapshotNormalizer: araç başına en yeni entity,<br/>geçersiz, zamansız ve gelecekten gelenler elenir
    I->>L: Apply (3 sa hafıza, 10 dk canlı)
    L-->>I: kaydedilecek konumlar + istemcilerin göreceği değişiklik
    I->>S: yeni konumları dosyaya yaz
    Note over I,S: Diske yazma yayından önce
    I->>H: değişikliği yayınla (upsert, removed, seq)
```

**b) Ayrı bir döngüde: diskten veritabanına**

```mermaid
sequenceDiagram
    autonumber
    participant W as PositionWriterWorker
    participant S as FilePositionSpool
    participant DB as Veritabanı

    loop spool'da parti oldukça
        W->>S: en eski partiyi oku
        W->>DB: INSERT … SELECT unnest(…) ON CONFLICT DO NOTHING
        DB-->>W: commit
        W->>S: partiyi sil
    end
```

Dikkat edilecek noktalar:

- Akıştaki ~8.400 entity yaklaşık 3.400 araca ait; aynı araç günün her seferi için ayrı entity olarak durur. Anahtar `codespace:vehicle.id`'dir, araç başına en yeni zaman damgalı entity kullanılır.
- Entur arkada birden çok sunucudan yanıt verir; ardışık iki okumada zaman geriye gidebilir. "Zaman damgası ilerlemediyse yok say" kuralı bunu çözer.
- **Diske yazma yayından önce** yapılır: bir istemcinin gördüğü her konum sonradan geçmişte de bulunur.
- Parti **commit edildikten sonra** spool'dan silinir. Arada çökme olursa parti yeniden yazılır; `(vehicle_key, recorded_at)` birincil anahtarı çift kaydı engeller.
- Okuma başarısız olursa (`FeedReadException`: bağlantı yok, 503, bozuk protobuf, zaman aşımı) hiçbir şey değişmez, sayaç artar, 3 sn sonra yeniden denenir.

## 5. Akış 2: Canlı pencere ve açılış

```mermaid
sequenceDiagram
    autonumber
    participant D as DatabaseInitializer
    participant DB as Veritabanı
    participant L as InMemoryLiveState
    participant I as IngestionService
    participant H as VehicleSocketHub

    D->>DB: migration + set_chunk_time_interval
    D->>DB: araç başına son konum (son 3 sa)
    D->>L: Apply(3 sa hafıza, 10 dk canlı)
    Note over L: Hepsi hatırlanır (tekrar kaydedilmez),<br/>yalnızca son 10 dk'dakiler haritaya çıkar

    loop her başarılı okumada
        I->>L: Apply(yeni konumlar, …)
        Note over L: 10 dk'dır susan araç canlıdan düşer<br/>→ istemcilere "removed".<br/>3 saati aşan araç hafızadan da silinir.
        L-->>H: değişiklik
    end
    Note over I,L: Okuma başarısızken Apply çağrılmaz:<br/>kaynak kapalıyken harita boşalmaz
```

- **Neden iki pencere:** seferi biten araçlar akışta saatlerce kalıyor (Vy'nin araç kimliği aslında sefer kimliği). Tek, 3 saatlik bir pencere haritanın yarısını hayalet araçlarla dolduruyordu. Canlılığı 10 dakikaya indirip saklamayı 3 saatte bırakmak, hem haritayı temizler hem de veri kaybetmez: 10 dakikadan eski ama henüz kaydedilmemiş bir konum (ör. kesintiden sonra) yine kaydedilir.
- **Neden 10 dakika:** normal bildirimlerin %99,9'u 220 sn içinde; ölçülen en uzun gerçek sessizlik 8,5 dk (Follobanen tüneli).

## 6. Akış 3: WebSocket, abonelik ve yeniden bağlanma

```mermaid
sequenceDiagram
    autonumber
    participant C as Tarayıcı (VehicleSocket)
    participant H as VehicleSocketHub / Session
    participant L as InMemoryLiveState

    C->>H: /ws/vehicles?access_token=…&bbox=w,s,e,n
    Note over H: Token doğrulanır (yoksa 401, bağlantı kurulmaz)
    H->>L: güncel durum (Register, yayınla aynı kilit altında)
    H-->>C: snapshot, aboneliğe göre süzülmüş (seq=120)
    H-->>C: alerts {version}

    loop her okuma döngüsünde
        H-->>C: delta (fromSeq, toSeq, upserted, removed)
        Note over H: Alandan çıkan araç → removed.<br/>Süzülünce boş kalan delta da gider<br/>(seq kesintisiz kalsın diye)
    end

    C->>H: subscribe {bbox, lines, keys}
    Note over C: Harita paylı alanın dışına çıktı,<br/>filtreye hat yazıldı ya da araç seçildi.<br/>En fazla 250 ms'de bir uygulanır
    H-->>C: yeni süzülmüş snapshot

    Note over C,H: Token süresi doldu
    H--xC: kapanış 4401
    C->>C: refresh (tek uçuş) → yeni access token
    C->>H: hemen yeniden bağlan → snapshot

    Note over C,H: Bağlantı koptu (ağ, backend yeniden başladı)
    Note over C: Rozet: "yeniden bağlanıyor", 1, 2, 4 … 15 sn
    Note over C: Bağlantı açılmadan kapandıysa (token reddedilmiş olabilir)<br/>önce token yenilenir
    C->>H: yeniden bağlan (aynı abonelik adreste)
    H-->>C: snapshot
```

- **Neden kayıpsız:** yayın olay değil **durum** taşır. Yeniden bağlanan istemci neyi kaçırdığını bilmek zorunda değil; yeni snapshot her aracın son halini içerir.
- **Abonelik kuralı:** sabitlenmiş araç (`keys`) her zaman gelir; değilse hat listesi doluysa yalnızca o hatlar; değilse alan varsa alandakiler; hiçbiri yoksa hepsi. Filtre boşken görünen alan (her yönde yarım ekran payla), filtre bir hatla eşleşiyorsa o hatlar, seçili araç her zaman.
- **Token yenileme:** imza anahtarı backend her açıldığında yeniden üretilebildiği için yeniden başlatmadan sonra eski token geçersiz olur; tarayıcı WebSocket'te 401'i göremez. Bu yüzden açılmadan kapanan bir bağlantıdan sonra token yenilenir; canlı veri backend hazır olduktan ~0,5 sn sonra geri gelir.

## 7. Akış 4: Yavaş istemci

```mermaid
sequenceDiagram
    autonumber
    participant H as VehicleSocketHub
    participant O as ClientOutbox (yavaş istemci)
    participant S as Session gönderim döngüsü
    participant C as Yavaş tarayıcı
    participant D as Diğer istemciler

    H->>O: değişiklik 21 (araç A, B)
    H->>D: değişiklik 21
    Note over C: Okumuyor, gönderim döngüsü soket yazmasında bekliyor
    H->>O: değişiklik 22 (araç A, C)
    H->>D: değişiklik 22 (gecikme yok)
    Note over O: Kuyruk büyümez: bekleyenler<br/>"araç başına son konum"da birleşir<br/>(A'nın 22'deki hali, B, C)
    S->>O: Take()
    Note over S: Abonelik süzmesi burada,<br/>kilit dışında yapılır
    S-->>C: tek delta, fromSeq=21 toSeq=22
    Note over S,C: Tek bir mesaj 15 sn'de gönderilemezse bağlantı kapatılır,<br/>istemci yeniden bağlanınca snapshot alır
```

Her istemcinin kendi giden kutusu ve kendi gönderim döngüsü vardır. Yayıncı yalnızca giden kutularına (belleğe) yazar, hiçbir soketin gönderimini beklemez; bu yüzden biri yavaşlayınca ne diğerleri ne de veri toplama etkilenir. Giden kutusu bir mesaj kuyruğu değil, "bu istemciye henüz gönderilmemiş durum"dur: yavaş istemcinin bellekteki maliyeti en fazla araç sayısı kadardır.

Yeni bağlanan istemcide de bir yarış vardır: snapshot alınırken bir değişiklik yayınlanırsa kaçabilir. Bunu önlemek için snapshot'ın alınması ve istemcinin listeye eklenmesi, yayınla aynı kilit altında yapılır; snapshot'a zaten girmiş bir değişiklik sonradan gelirse sıra numarasından tanınıp atlanır. Abonelik değişince yeni snapshot da aynı kilit altında sıraya konur.

## 8. Akış 5: Veritabanı durup yeniden başlıyor

```mermaid
sequenceDiagram
    autonumber
    participant I as IngestionService
    participant S as FilePositionSpool
    participant W as PositionWriterWorker
    participant DB as Veritabanı
    participant C as Tarayıcı

    I->>S: parti 41
    W->>DB: parti 41
    DB-->>W: commit
    Note over DB: Konteyner durdu

    I->>S: parti 42, 43, 44 … (birikir)
    W-xDB: parti 42 (hata)
    Note over W: 1, 2, 4 … 15 sn aralıklarla yeniden dener
    Note over C: Harita canlı kalır (yayın bellekten).<br/>/health/ready "Degraded",<br/>sistem durumunda bekleyen parti sayısı artar

    Note over DB: Konteyner yeniden başladı<br/>(restart: unless-stopped)
    W->>DB: parti 42, 43, 44 … sırayla
    Note over S: Spool boşalır, aradaki konumlar boşluksuz yazılır
```

Denendi: 45 sn'lik kesintide 8 parti birikti ve ~4 sn'de boşaldı. Docker ana makinesi yeniden başlatıldığında backend açıldı ama veritabanı açılmadı; backend 13 partiyi tampona yazdı ve veritabanı başlatılınca kayıpsız boşalttı. Bundan sonra veritabanına da `restart: unless-stopped` eklendi.

## 9. Akış 6: Saklama ve silme

```mermaid
sequenceDiagram
    autonumber
    participant D as DatabaseInitializer
    participant R as RetentionWorker
    participant DB as TimescaleDB

    D->>DB: set_chunk_time_interval(saklama / 12)
    Note over DB: 3 saat saklama → 15 dk'lık chunk'lar
    loop chunk süresinin yarısında bir (en fazla 5 dk)
        R->>DB: drop_chunks(older_than = şimdi − saklama)
        Note over DB: Eski chunk tablo olarak düşürülür.<br/>Satır satır DELETE yok.
        DB-->>R: düşürülen chunk'lar
    end
```

Satır satır `DELETE` milyonlarca satırı tek tek işaretler, tabloyu şişirir ve vacuum yükü yaratır. Chunk düşürmek bir dosyayı kaldırmak gibidir: süresi veri miktarından bağımsızdır ve yazma ya da okuma yapan sorguları bekletmez. Bir chunk ancak tamamı pencerenin dışına çıkınca düşer; bu yüzden en eski kayıt 3 saatten en fazla bir chunk ve bir tarama aralığı kadar eski olabilir.

## 10. Akış 7: Kimlik doğrulama

```mermaid
sequenceDiagram
    autonumber
    participant B as Tarayıcı
    participant A as AuthController / AuthService
    participant DB as Veritabanı
    participant API as Diğer uç noktalar

    B->>A: POST /api/auth/login {email, password}
    A->>DB: kullanıcı + şifre hash'i (PBKDF2)
    A->>DB: refresh token'ın SHA-256 hash'i (yeni aile)
    A-->>B: {accessToken (JWT, 15 dk)} + Set-Cookie refresh_token<br/>(HttpOnly, SameSite=Strict, Path=/api/auth)
    Note over B: Access token yalnızca bellekte

    B->>API: GET … Authorization: Bearer
    API-->>B: 200 (ya da süresi dolduysa 401)
    B->>A: POST /api/auth/refresh (cookie, tek uçuş)
    A->>DB: eski token iptal + yenisi aynı ailede
    A-->>B: yeni access token + yeni cookie
    B->>API: isteği tekrarla

    Note over B,A: Sayfa yenilendi: bellek boş
    B->>A: POST /api/auth/refresh (sessiz giriş)
    Note over B: Backend erişilemezse oturum silinmez,<br/>sessiz giriş backend gelene kadar tekrar denenir

    Note over A,DB: Zaten kullanılmış bir refresh token gelirse<br/>bütün aile iptal edilir → 401 → giriş ekranı
```

- **Kapsam:** varsayılan politika "giriş yapmış kullanıcı"; işaretlenmeyi unutulan bir uç nokta da kapalı kalır. Açık olanlar: giriş/kayıt/refresh/çıkış, `/health`, `/health/ready`, Swagger. `GET /api/status` yalnızca `admin`.
- **Kaba kuvvet:** yalnızca başarısız girişler sayılır (adres başına 5 dk'da 10).
- **Admin hesabı:** açılışta yapılandırmadan oluşturulur (`AdminSeeder`).

## 11. Akış 8: Uyarılar ve sefer takibi

**a) Uyarılar**

```mermaid
sequenceDiagram
    autonumber
    participant A as AlertService<br/>(AlertPollingWorker, 30 sn)
    participant H as VehicleSocketHub
    participant B as Tarayıcı

    A->>A: uyarıları oku, dili normalleştir ya da tahmin et
    Note over A: İçerik değiştiyse version artar
    A->>H: yeni version
    H-->>B: alerts {version}
    B->>A: GET /api/alerts?lineId=&stopId=
    A-->>B: güncel liste
```

Uyarıların %60'ı yalnızca sefer adı verdiği için, hat filtresinin çalışması amacıyla her seferin hattı Journey Planner'dan bir kez sorulur (önbellekli, en fazla 4 paralel istek).

**b) Sefer takibi**

```mermaid
sequenceDiagram
    autonumber
    participant B as Tarayıcı
    participant T as TripUpdateService<br/>(TripUpdatePollingWorker, 30 sn)
    participant J as TripRouteService<br/>(Journey Planner)

    Note over T: Akış (~12 MB) sefer başına durak gecikmelerini verir,<br/>mutlak saat vermez. ±6 saat dışındakiler atılır
    B->>T: GET /api/trips/{tripId}/progress
    T->>J: planlanan durak saatleri (6 sa önbellek)
    J-->>T: durak listesi + saatler
    T-->>B: tahmini saat = planlanan + gecikme (Norveç saati)
```

## 12. Veri kaybı senaryoları

| Senaryo | Ne olur | Kayıp |
|---|---|---|
| Veritabanı birkaç dakika kapalı | Partiler diskte birikir, veritabanı dönünce sırayla yazılır | Yok |
| Uygulama yeniden başlar | Spool'da kalanlar tekrar yazılır, çift kayıt oluşmaz; canlı durum veritabanından ve ilk okumadan kurulur; tarayıcılar kendiliğinden yeniden bağlanır | Kapalı kalınan sürede okunamayan ara konumlar |
| Entur erişilemez, bozuk ya da yarıda donan yanıt | Okuma başarısız sayılır, döngü sürer; harita son durumu korur | **Var:** o aralıktaki ara konumlar geri getirilemez, akış geçmişi yeniden sunmuyor. Kaynak dönünce güncel durum eksiksiz gelir |
| İstemci bağlantıyı koparır | Yeniden bağlanır, aboneliğine göre süzülmüş snapshot alır | Yok |
| İstemci çok yavaş okur | Bekleyenler araç başına son konumda birleşir; yetişince tek delta alır; 15 sn'de bir mesajı alamazsa kapatılır | Yok (ara kareleri görmez, son durumu görür) |
| Disk tamponuna yazılamaz (disk dolu) | O döngünün konumları haritada görünür ama veritabanına girmez | **Var** (bilinen eksik: tamponun üst sınırı yok) |

## 13. Frontend'de veri nerede durur

```mermaid
flowchart LR
    subgraph Canli["Canlı veri"]
        direction TB
        WS["WebSocket mesajları"] --> LS["LiveVehicleStore<br/>React dışında"]
        LS --> MapL["Harita<br/>kare başına bir kez çizilir"]
        LS --> Panel["Seçili aracın paneli"]
    end

    subgraph Sunucu["Sunucu verisi"]
        direction TB
        REST["REST yanıtları"] --> TQ["TanStack Query<br/>önbellek, yenileme"]
        TQ --> Ekranlar["Geçmiş · uyarılar · gecikme<br/>güzergah · sistem durumu"]
    end

    subgraph Arayuz["Arayüz durumu"]
        direction TB
        URL["URL<br/>filtre, seçili araç"]
        Zs["Zustand<br/>oturum (access token), bağlantı,<br/>abonelik, ekran sınırları"]
    end
```

Üç alan birbirinden bağımsızdır ve aralarındaki bağlantılar küçüktür: WebSocket'ten gelen `alerts {version}` uyarı sorgusunu yeniden çektirir; harita hareketi ve filtre, Zustand'daki aboneliği değiştirir, abonelik de WebSocket'e `subscribe` olarak gider; access token hem REST isteklerine hem WebSocket bağlantısına eklenir.

| Veri türü | Nerede | Neden |
|---|---|---|
| Canlı araç konumları | React dışında bir store | Birkaç saniyede bir binlerce konum değişiyor; React render'ını tetiklememeli |
| Geçmiş, uyarılar, gecikme, güzergah, sistem durumu | TanStack Query | Sunucu verisi: önbellek, sayfalama, periyodik yenileme hazır gelir |
| Bağlantı durumu, abonelik, ekran sınırları | Zustand | Küçük, arayüze ait durum |
| Access token | Zustand, yalnızca bellekte | Kalıcı bir yerde durmaz; yenilemede HttpOnly cookie'den geri gelir |
| Filtre, seçili araç | URL | Sayfa yenilenince ve link paylaşılınca korunmalı |
