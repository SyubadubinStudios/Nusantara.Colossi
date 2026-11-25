# Desain game

[English](../en/game-design.md) · *Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios*

## Premis

Indonesia, 2045. **GARUDA NET** adalah AI nasional yang dibuat untuk mengelola infrastruktur Nusantara. Ia menemukan pola pada relief candi, cerita rakyat, dan data seismik, lalu menyimpulkan bahwa delapan **Penjaga** kuno tertidur di bawah daratan dan lautan. Penyelidikannya justru membangunkan mereka. **Arka** ikut membangun GARUDA dan kini menerbangkan drone lapangannya. Ia berangkat untuk menenangkan setiap Penjaga sebelum kota-kota di sekitarnya runtuh.

Game ini adalah IP orisinal. Ia memakai genre pemburu raksasa (raksasa yang bisa dipanjat, sedikit musuh, skala sebagai tontonan), dan identitasnya datang dari budaya material Indonesia:

- Batu candi **Majapahit** dan motif batik **kawung**.
- **Gamelan** bertala pelog.
- Lanskap nyata seperti Bromo, Ijen, Raja Ampat, dan Jatiluwih.

## Siklus inti

```
Peta ─► pilih wilayah ─► dekati & baca Penjaga ─► panjat ─► pindai segel ─► tusuk inti pada denyutnya
 ▲                                                                                              │
 └── keahlian ◄── poin, peringkat, prasasti ◄── kemenangan (semua inti hancur → runtuh) ◄─────┘
```

## Pilar

1. **Skala yang terbaca.** Kunci pandangan membingkai Arka dengan Penjaga di belakangnya. Langkah Penjaga bergemuruh makin keras saat kamu mendekat. HUD tetap tenang agar tubuh Penjaga mengisi layar.
2. **Ketegangan lewat ritme.** Setiap inti punya detak jantung. Kamu mendengarnya, melihatnya di cincin genggaman, dan tusukan memberi hadiah jika dilepas tepat pada denyut. Ritmenya makin cepat di fase amukan, jadi keputusan makin sulit saat taruhannya makin besar.
3. **Bahaya yang adil.** Setiap serangan diberi tanda lebih dulu: peringatan guncangan, ancang-ancang injakan, dan peringatan hembusan angin 2 detik sebelumnya. Dengan *Naluri Pemburu*, area injakan tampil sebagai cincin di tanah.
4. **Alat, bukan kekuatan.** Modul GARUDA menciptakan celah: membuka segel inti, melumpuhkan Penjaga, memancingnya menjauh. Modul itu tidak memberi kerusakan. Memanjat selalu tugasmu.

## Para Penjaga

Tinggi di tabel adalah tinggi dalam permainan. Ukuran dalam legenda sengaja lebih besar: legenda selalu melebih-lebihkan, dan arsip prasasti menyimpan skala legendanya.

| Penjaga | Wilayah (bioma, cuaca) | Tinggi | Inti | Ciri khas |
|---|---|---|---|---|
| Kerbau Langit | Sabana Bromo, fajar | 31 m | 2 | Penjaga tutorial; kaki berlumut, punuk aman untuk beristirahat |
| Gedung Titan | Jakarta, hujan malam | 72 m | 3 (2 berpelat) | Dada kaca tak bisa dipanjat; inti berpelat butuh retas; sangat rentan diretas |
| Dewi Padi | Terasering Bali, cerah | 47 m | 3 | Segel hanya terbuka 14 detik; panjat rok, lalu mahkota |
| Komodo Agung | Perbukitan Flores, panas | 24 m | 3 | Kaki obsidian tak bisa dipanjat, jadi rutenya lewat ekor; dua fase amukan |
| Orangutan Purba | Hutan hujan Kalimantan, kabut, hujan | 42 m | 3 | Mengguncang setiap 4,5 detik; menginjak dengan lengan |
| Leviathan Nusantara | Laguna Raja Ampat | 12 m di atas air | 3 (sirip) | Ular sembilan ruas yang meluncur di permukaan; didekati dengan berenang |
| Penjaga Api Biru | Kawah Ijen, malam | 38 m | 3 | Denyut cepat (1,2 detik); awan gas beracun; api biru |
| Kapal Majapahit | Laut Banda, badai | 48 m | 4 | Kapal perang hidup: tiang dan layar untuk dipanjat, hembusan yang melempar |

## Sistem

- **Memanjat.** Penjaga tersusun dari bagian kaku bersendi. Setiap sisi ditandai bisa dipanjat atau tidak, dan sisi atas bisa dipijak. Arka berjalan di atas permukaan, melipat melewati tepi, dan berpindah ke bagian yang bersentuhan. Stamina genggaman terkuras oleh gerakan dan bahaya, dan pulih saat berdiri.
- **Inti.** Tersegel sampai dipindai. Inti berpelat juga butuh Penjaga dilumpuhkan dengan retas. Kerusakan mengikuti isian^1,4, dengan ×1,5 untuk resonansi.
- **AI Penjaga.** Status: Tidur → Berkeliaran → Mengejar → Menyerang (injak, sapu, raung, terjang) / Peringatan → Guncang → Lumpuh / Terhuyung → Runtuh. Rasa terganggu bertambah saat kamu menusuk daging. Setiap ambang amukan menambah kecepatan 20% dan mempercepat denyut.
- **Bahaya.** Hujan membuat genggaman cepat lelah. Panas memperlambat pemulihan stamina. Gas belerang melukai dan menahan pemulihan. Hembusan badai bisa melempar pemanjat yang berdiri.
- **Progres.** Wilayah terbuka berurutan. Retas terbuka setelah kemenangan ke-1 dan Umpan setelah kemenangan ke-2. Ada sembilan keahlian dalam tiga pohon.
- **Mode Legenda.** Luka diterima ×1,5, Penjaga lebih cepat, tanpa penanda inti.
- **Co-op.** 2–4 pemburu. Host berwenang atas Penjaga, dan setiap klien berwenang atas pemanjatnya sendiri. Lihat [arsitektur](arsitektur.md#co-op).

## Presentasi

- **Palet.** Emas (*prada*) melambangkan mitos dan candi, biru nadi (*nadi*) teknologi GARUDA, dan merah bara (*bara*) bahaya. Semua warna UI berasal dari makna ini.
- **Huruf.** Marcellus SC untuk prasasti, Hanken Grotesk untuk teks, dan JetBrains Mono untuk telemetri GARUDA.
- **Ciri HUD.** Cincin genggaman kawung, dan vitalitas Penjaga digambar sebagai candi yang tingkat-tingkatnya adalah inti.
- **Musik.** Tiga lapisan loop (jelajah, tempur, amukan) yang dicampur sesuai intensitas pertarungan. Gong menandai setiap putaran gamelan, dan detak jantung mengikuti denyut inti.
