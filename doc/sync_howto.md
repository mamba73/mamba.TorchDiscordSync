| Naredba | Rezultat |
| :--- | :--- |
| ```python sync.py``` | Šalje sve (kôd + alati) na tvoj **privatni (private)** GitHub i ažurira verziju u README. |
| ```python sync.py --zip``` | Radi samo **Source ZIP** (predefinirane datoteke) u lokalnom folderu projekta. |
| ```python sync.py --full-backup``` | Radi **kompletan ZIP** cijelog foldera u direktorij iznad (```..```). |
| ```python sync.py --release``` | Čisti kôd i šalje ga na **javni (origin)** GitHub (bez ZIP uploada). |
| ```python sync.py --deploy``` | **Sve u jednom:** Čisti kôd -> Javni GitHub -> ZIP s DLL-ovima -> GitHub Release. |
