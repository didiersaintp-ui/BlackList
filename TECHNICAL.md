# Documentation Technique - EMV Blacklist System

## Vue d'ensemble

Ce document décrit l'architecture technique et les détails d'implémentation du système de gestion de listes noires EMV PAN.

## Architecture des Filtres

### 1. Cuckoo Filter

#### Principe de Fonctionnement

Le Cuckoo Filter utilise le "cuckoo hashing" avec deux fonctions de hash. Chaque élément peut résider dans deux positions possibles:

```
Position 1: h1(x) mod m
Position 2: h1(x) XOR h2(fingerprint(x)) mod m
```

#### Structure de Données

```csharp
class Bucket {
    byte[][] Fingerprints; // 4 empreintes par bucket
}

Bucket[] buckets; // Table de buckets
```

#### Paramètres de Configuration

- **Capacity**: 1,200,000 (pour 1M d'éléments + buffer)
- **Bucket Size**: 4 (slots par bucket)
- **Fingerprint Size**: 2 bytes (16 bits)

#### Calcul du Taux de Faux Positifs

```
FP Rate ≈ 2 * bucket_size * 2^(-fingerprint_bits)
        ≈ 2 * 4 * 2^(-16)
        ≈ 0.0122% (1.22 × 10^-4)
```

#### Taille Mémoire Théorique

```
Size = num_buckets * bucket_size * fingerprint_size
     = (1,200,000 / 4) * 4 * 2 bytes
     = 2,400,000 bytes
     ≈ 2.29 MB
```

### 2. Quotient Filter

#### Principe de Fonctionnement

Le Quotient Filter divise un hash en deux parties:
- **Quotient**: bits de poids fort (index du slot)
- **Remainder**: bits de poids faible (stocké dans le slot)

```
hash(x) = quotient | remainder
index = quotient
store = remainder
```

#### Métadonnées de Slot

Chaque slot contient:
- `remainder`: reste du hash (16 bits)
- `is_occupied`: bit indiquant si le slot canonique est utilisé
- `is_continuation`: bit indiquant continuation d'un run
- `is_shifted`: bit indiquant si l'élément a été déplacé

#### Paramètres de Configuration

- **Capacity**: 1,200,000
- **Quotient Bits**: log2(1,200,000) ≈ 21 bits
- **Remainder Bits**: 16 bits

#### Taux de Faux Positifs

```
FP Rate = 2^(-remainder_bits)
        = 2^(-16)
        ≈ 0.00152% (1.52 × 10^-5)
```

#### Taille Mémoire

```
Size = num_slots * (remainder_bits + 3_metadata_bits)
     = 2^21 * (16 + 3) bits
     = 2,097,152 * 19 bits
     ≈ 4.98 MB
```

### 3. Hash Table + Golomb

#### Principe de Fonctionnement

Combinaison de deux structures:

1. **Hash Table**: Stockage exact (zero faux positifs)
2. **Golomb Coding**: Compression pour transmission

#### Hash Table

```csharp
HashSet<ulong> hashTable; // Hash 64-bit de chaque PAN
```

- **Taille par élément**: 8 bytes
- **Taux de faux positifs**: 0% (hash table exacte)

#### Golomb Coding

Encode les deltas entre hashes triés:

```
Sorted: [h1, h2, h3, ...]
Deltas: [h1, h2-h1, h3-h2, ...]
```

Chaque delta est encodé en:
- **Quotient**: unaire (q bits)
- **Remainder**: binaire (log2(m) bits)

#### Paramètres

- **Golomb Parameter (m)**: 256
- **Average bits per delta**: ≈ 9-10 bits

#### Taille Mémoire

**En mémoire (Hash Table)**:
```
Size = 1,000,000 * 8 bytes
     = 8,000,000 bytes
     ≈ 7.63 MB
```

**Après compression (Golomb)**:
```
Size ≈ 1,000,000 * 10 bits
     = 10,000,000 bits
     = 1,250,000 bytes
     ≈ 1.19 MB
```

## Synchronisation Delta

### Architecture

```
┌─────────┐        Delta (1/sec)         ┌────────┐
│   API   │───────────────────────────▶  │ Queue  │
└─────────┘                               └────────┘
                                              │
                                              │ Poll (10s)
                                              ▼
                                         ┌────────┐
                                         │ Client │
                                         └────────┘
```

### Génération de Deltas (API)

Toutes les secondes, l'API génère un delta:

```csharp
Timer deltaGenerator = new Timer(_ => {
    var delta = new BlacklistDelta {
        Timestamp = CurrentUnixTime(),
        AddedPans = GenerateRandomAdditions(1),
        RemovedPans = GenerateRandomRemovals(1)
    };
    deltaQueue.Enqueue(delta);
}, null, 1000ms, 1000ms);
```

### Application de Deltas (Client)

Toutes les 10 secondes, le client récupère les deltas:

```csharp
Timer syncTimer = new Timer(_ => {
    var request = new DeltaRequest {
        LastTimestamp = lastTimestamp,
        Count = 10  // 10 secondes * 1 delta/sec
    };

    var response = await api.GetDeltas(request);

    foreach (var delta in response.Deltas) {
        foreach (var pan in delta.AddedPans)
            filter.Add(pan);
        foreach (var pan in delta.RemovedPans)
            filter.Remove(pan);

        lastTimestamp = delta.Timestamp;
    }
}, null, 10000ms, 10000ms);
```

## Performance Attendue

### Insertion (1M éléments)

| Filter            | Temps   | Throughput      |
|-------------------|---------|-----------------|
| Cuckoo Filter     | ~2-3s   | ~400K ops/sec   |
| Quotient Filter   | ~3-4s   | ~300K ops/sec   |
| Hash Table Golomb | ~1-2s   | ~600K ops/sec   |

### Recherche (10K lookups)

| Filter            | Temps    | Throughput      |
|-------------------|----------|-----------------|
| Cuckoo Filter     | ~5ms     | ~2M ops/sec     |
| Quotient Filter   | ~8ms     | ~1.25M ops/sec  |
| Hash Table Golomb | ~3ms     | ~3.3M ops/sec   |

### Taille Sérialisée

| Filter            | Taille    | Bytes/Item |
|-------------------|-----------|------------|
| Cuckoo Filter     | ~2.3 MB   | ~2.3       |
| Quotient Filter   | ~5.0 MB   | ~5.0       |
| Hash Table Golomb | ~1.2 MB   | ~1.2       |

### Taux de Faux Positifs

| Filter            | FP Rate (Théorique) | FP Rate (Mesuré) |
|-------------------|---------------------|------------------|
| Cuckoo Filter     | 0.012%              | ~0.01-0.02%      |
| Quotient Filter   | 0.0015%             | ~0.001-0.003%    |
| Hash Table Golomb | 0%                  | 0%               |

## Optimisations Implémentées

### 1. Hashing Rapide

Utilisation de `XxHash64` pour performance:
- 2-3x plus rapide que SHA-256
- Excellente distribution
- Faible taux de collision

```csharp
using System.IO.Hashing;

var hash = XxHash64.Hash(Encoding.UTF8.GetBytes(pan));
```

### 2. Golomb Coding Optimisé

Encodage bit-level efficace:

```csharp
class BitWriter {
    private byte currentByte;
    private int bitPosition;

    public void WriteBit(int bit) {
        currentByte |= (byte)(bit << (7 - bitPosition));
        bitPosition++;
        if (bitPosition == 8) {
            Flush();
        }
    }
}
```

### 3. Lock-Free Reads

Utilisation de `ConcurrentQueue` pour deltas:
- Pas de locks en lecture
- Scalabilité multi-thread
- Performance optimale

### 4. Memory Pool

Pour Quotient Filter:
- Réutilisation des slots
- Réduction GC pressure
- Meilleure localité cache

## Tests et Validation

### Tests Unitaires

```bash
dotnet test tests/EMVBlacklist.Tests/
```

Couvre:
- ✅ Add/Contains/Remove pour chaque filtre
- ✅ Sérialisation/Désérialisation
- ✅ Comptage d'éléments
- ✅ Large datasets (10K+ éléments)
- ✅ Taux de faux positifs
- ✅ Métadonnées

### Tests d'Intégration

Via Docker Compose:
- ✅ Synchronisation API ↔ Client
- ✅ Deltas en temps réel
- ✅ Validation croisée
- ✅ Récupération après erreur

### Benchmarks

```bash
./run-benchmarks.sh  # Linux/Mac
run-benchmarks.bat   # Windows
```

Mesure:
- Temps d'insertion
- Temps de recherche
- Taille mémoire
- Taux de faux positifs
- Performance deltas

## Considérations de Production

### Scalabilité

**Horizontal Scaling**:
- Partitionner les PANs par range
- Load balancer devant les APIs
- Shared Redis pour deltas

**Vertical Scaling**:
- 1M PANs ≈ 5-10 MB RAM
- 10M PANs ≈ 50-100 MB RAM
- 100M PANs ≈ 500MB-1GB RAM

### Haute Disponibilité

- Redis Cluster pour persistance
- Multiple API replicas
- Health checks automatiques
- Graceful degradation

### Monitoring

Métriques clés:
- Latency P50/P95/P99 des opérations
- Taille du filtre
- Taux de faux positifs mesuré
- Deltas en retard
- Memory usage

### Sécurité

**En Production**:
1. Hash PANs avec HMAC-SHA256 + secret
2. TLS pour toutes communications
3. Rate limiting sur API
4. Audit logs complets
5. PCI DSS compliance

## Comparaison des Use Cases

### Choisir Cuckoo Filter

✅ Bon pour:
- Balance performance/mémoire
- Besoin de suppressions
- Taux FP acceptable (0.01%)

❌ Éviter si:
- Besoin zero faux positifs
- Insertions > 90% capacité

### Choisir Quotient Filter

✅ Bon pour:
- Cache performance critique
- Très faible taux FP requis
- Iteration sur éléments

❌ Éviter si:
- Mémoire limitée
- Implémentation simple requise

### Choisir Hash Table + Golomb

✅ Bon pour:
- Zero faux positifs requis (compliance)
- Transmission réseau fréquente
- Budget mémoire minimal

❌ Éviter si:
- Modifications ultra-fréquentes
- Latency critique (<1ms)

## Références

- [Cuckoo Filter Paper](https://www.cs.cmu.edu/~dga/papers/cuckoo-conext2014.pdf)
- [Quotient Filter Paper](https://dl.acm.org/doi/10.1145/2674005.2674994)
- [Golomb Coding](https://en.wikipedia.org/wiki/Golomb_coding)
- [PCI DSS Requirements](https://www.pcisecuritystandards.org/)

## Contact & Support

Pour questions techniques:
- Issues GitHub
- Code review via PR
- Documentation inline

