# Masukan 2026-10-23: kotak otomatis, preset bentuk, node kurva, hapus preset bahan

Sumber: masukan user/leader setelah audit 2026-10-23 ("fitur yang sekarang sudah bagus") dan jawaban user di sesi yang sama.
Status: disetujui user 2026-10-23. Implementasi selesai 2026-10-23. Hasil uji: PRD F23, F25, F26; UJI-GARIS-POTONG S12, S14, S15;
e2e CUT-14, CUT-16, CUT-17, EDIT-17, EDIT-18, HW-17; validasi A7.

## Kebutuhan dan keputusan

| # | Kebutuhan | Keputusan |
| --- | --- | --- |
| 1 | Desain berbentuk kotak belum otomatis mendapat garis kotak | Foto atau gambar kotak tanpa transparansi otomatis mendapat garis kotak bersudut tajam (4 node), tanpa operator memilih apa pun |
| 2 | Node bisa ditambah pada garis lurus maupun kurva | Editor seperti CorelDRAW: segmen lurus atau kurva Bezier, handle per node, klik di garis atau kurva menambah node tanpa mengubah bentuk, segmen bisa diubah lurus ↔ kurva |
| 3 | Preset bentuk garis potong: kotak, lingkaran, oval, segitiga, bintang, hati, dan lainnya | Dipilih di editor garis potong. Bentuk mengelilingi artwork ditambah offset, sehingga artwork tidak terpotong. Ditambah kotak sudut bulat dan segi enam |
| 4 | Preset bahan tidak diperlukan | Pilihan preset bahan dihapus dari aplikasi, uji, dan dokumen |

## Desain

### Model garis: node kurva

- Garis manual menjadi `List<Node[]>` (mm relatif terhadap artwork). `Node(P, In, Out, Smooth)`: segmen i→i+1 lurus
  bila `Out` node i sama dengan `P` dan `In` node berikutnya sama dengan `P`-nya. Selain itu, segmen berupa cubic Bezier.
- Garis otomatis (kurva Potrace) dibaca langsung menjadi node kurva. Simplify mem-fit ulang kurva otomatis dengan galat
  maksimal eps (metode Schneider), sehingga hasilnya tetap kurva dan node berkurang bila digeser ke Simple.
- Export PDF tidak berubah: Bezier sudah ditulis sebagai kurva.

### Editor

- Klik node untuk memilih, lalu seret untuk memindahkannya beserta handle. Node terpilih menampilkan handle In dan Out.
  Menyeret handle mengubah lengkung; pada node halus, handle seberang ikut segaris.
- Klik di garis atau kurva menambah node (de Casteljau, bentuk tetap). Delete menghapus node.
- Tombol: Jadikan kurva, Jadikan garis, Node halus, Node sudut.
- Panel Bentuk: Otomatis dan 8 bentuk. Bentuk yang dipilih langsung tampil. Simpan tanpa mengedit node menyimpan
  bentuknya. Bila node diedit, hasilnya disimpan sebagai garis manual.

### Kotak otomatis

- Silhouette dianggap kotak bila semua piksel di tepi mask adalah artwork dan isinya ≥ 99,5 %. Contohnya JPG/PNG opak,
  PNG kotak, dan PDF berlatar kotak putih. Kotak bersudut bulat yang transparan di sudut tidak termasuk.
- Garis kiss = kotak artwork ± offset kiss. Garis die = kotak ± offset die, minimal kiss + 1 piksel. Halus tidak dipakai.

### Preset bentuk

- Bentuk: kotak, kotak sudut bulat (radius 3 mm), lingkaran, oval, segitiga sama sisi, bintang 5 sudut, hati, segi enam.
- Bentuk mengelilingi artwork: titik uji adalah hull cembung silhouette yang diperluas sejauh offset.
  - Kotak = bbox ± offset.
  - Lingkaran = lingkaran minimal + offset.
  - Oval = elips dengan rasio bbox.
  - Bentuk lain = skala seragam minimal.
- Die memakai bentuk dan pusat yang sama, dengan offset die, dan minimal kiss + 1 piksel.
- Bentuk disimpan per desain (`Sticker.Shape`) dan dihitung ulang saat offset, lebar, atau susun berubah.

### Hapus preset bahan

- Kotak pilihan, data preset, pemulihannya di undo, cek selfcheck, langkah uji UI EDIT-09, dan kasus e2e EDIT-09 dihapus.
  Catatan riset nilai preset (RESEARCH bagian 13) tetap ada sebagai riwayat.

## Uji

- Selfcheck S14: kotak otomatis. Selfcheck S15: preset bentuk. Selfcheck S12 diperluas untuk kurva.
- Validasi dijalankan sebelum dan sesudah kotak otomatis. Untuk desain kotak, kriteria Halus dan "tanpa garis lurus"
  tidak berlaku, karena garis kotak memang 4 garis lurus menurut keputusan #1.
- Uji UI tanpa mouse untuk tombol bentuk. Seret node dan handle dengan mouse belum bisa diuji otomatis.
- Potong fisik di Graphtec untuk bentuk dan kurva manual menunggu PC operator.

## Di luar cakupan

- Memotong di dalam artwork (crop foto), memutar bentuk, mengetik ukuran bentuk, bentuk dari file.
