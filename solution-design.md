# Nusantara Colossi

## Game Action-Adventure Boss Hunter Terinspirasi Shadow of the Colossus dengan Setting Indonesia Modern

---

## High Concept

**Nusantara Colossi** adalah game action-adventure open world semi-linear yang berlatar Indonesia modern alternatif, di mana muncul makhluk-makhluk raksasa kuno ("Colossi") yang tertidur di berbagai wilayah Nusantara.

Tidak seperti Shadow of the Colossus yang berlatar dunia fantasi kosong, dunia Nusantara Colossi tetap hidup:

- Kota metropolitan modern
- Desa wisata
- Hutan tropis
- Pantai eksotis
- Kawasan industri
- Sawah bertingkat
- Pegunungan vulkanik
- Savana

Masing-masing Colossi menyatu dengan lingkungan.

### Contoh Colossi

- Colossus berbentuk gedung pencakar langit hidup di Jakarta
- Colossus berbentuk naga batu di Raja Ampat
- Colossus berbentuk kerbau raksasa di sawah Bali
- Colossus berbentuk komodo gunung di Flores
- Colossus berbentuk kapal kuno raksasa di Laut Banda

---

# Cerita

Tahun 2045.

Indonesia meluncurkan proyek AI nasional bernama **GARUDA NET**.

Sebuah jaringan AI kuantum yang menghubungkan satelit, data center, sensor lingkungan, dan infrastruktur nasional.

Ketika eksperimen berjalan, terbangun entitas kuno yang telah tertidur sejak peradaban Nusantara purba.

Mereka disebut **Penjaga Nusantara (The Colossi)**.

Pemain berperan sebagai **Arka**, explorer, programmer, drone pilot, dan mantan engineer AI.

---

# Area Dunia dan Boss

## Jakarta Hyper City
- Inspirasi: Sudirman, Thamrin, SCBD
- Boss: Gedung Titan
- Tinggi: 400 meter
- Gameplay: Parkour, drone hack, lift dinamis

## Bromo Savannah
- Boss: Kerbau Langit
- Ukuran: 250 meter
- Gameplay: Motor trail, climbing, boss chase

## Bali Rice Terraces
- Boss: Dewi Padi
- Gameplay: Puzzle irigasi dan climbing

## Raja Ampat
- Boss: Leviathan Nusantara
- Panjang: 1 km
- Gameplay: Diving, boat chase, climbing

## Kalimantan Rainforest
- Boss: Orangutan Purba
- Gameplay: Survival dan grappling

## Flores
- Boss: Komodo Agung
- Multi-phase battle

## Ijen
- Boss: Penjaga Api Biru
- Gameplay terkait suhu ekstrem dan gas beracun

## Laut Banda
- Boss: Kapal Majapahit
- Gameplay eksplorasi laut dan badai

---

# Gameplay Loop

```text
Explore
↓
Discover Clue
↓
Find Colossus
↓
Puzzle Battle
↓
Climb Colossus
↓
Destroy Core
↓
Unlock History
↓
New Area
```

---

# Fitur Gameplay

## Drone Companion GARUDA
- Scan objek
- Weak point scanning
- Mapping
- AR Vision

## Parkour
- Wall run
- Climbing
- Swing

## Vehicles
- Motor trail
- Mobil offroad
- Perahu
- Helikopter ringan
- Jet ski

## Dynamic Weather
- Hujan tropis
- Kabut hutan
- Badai laut
- Abu vulkanik

---

# RPG Ringan

## Explorer
- Climbing stamina
- Swimming

## Engineer
- Drone upgrade
- Hacking

## Survivalist
- Resistance
- Tracking

---

# Multiplayer

## Expedition Co-op

2–4 pemain.

Misi:
- Menemukan Colossi
- Menyelesaikan puzzle bersama
- Raid boss raksasa

---

# Teknologi

| Layer | Teknologi |
|---------|---------|
| Language | C# 14 (.NET 10) |
| UI | Blazor Hybrid |
| Desktop Windows | WPF |
| Desktop Mac/Linux | Avalonia UI |
| Mobile | .NET MAUI |
| Rendering | Three.js |
| Physics | Babylon.js Physics + Havok |
| Networking | SignalR |
| Save | LiteDB |
| Cloud Save | Azure Cosmos DB |
| Telemetry | Azure Application Insights |
| AI NPC | Semantic Kernel |
| Voice NPC | Azure AI Speech |
| Asset Streaming | Azure Blob Storage |
| Matchmaking | Azure PlayFab |

---

# Arsitektur Solusi

```text
+----------------------+
|      Blazor UI       |
+----------+-----------+
           |
+----------v-----------+
|     Game Services    |
+----------+-----------+
           |
+----------v-----------+
|     ECS Runtime      |
+----------+-----------+
           |
+----------v-----------+
|      Three.js        |
+----------+-----------+
           |
+----------v-----------+
| Babylon Physics      |
+----------------------+
```

---

# Struktur Solution

```text
NusantaraColossi.sln

src/
├── Shared/
├── Game.Core/
├── Game.ECS/
├── Game.Physics/
├── Game.AI/
├── Game.Rendering/
├── Game.Networking/
├── Game.SaveSystem/
├── Game.Audio/
├── Game.World/
├── Game.Quests/
├── Game.Colossi/
├── Game.Server/
├── App.Windows.WPF/
├── App.Maui/
├── App.Avalonia/
└── App.Blazor.Shared/
```
