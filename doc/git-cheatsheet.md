1. Odaberite metodu resetiranja 
Najprije pronađite hash (ID) commita na koji se želite vratiti pomoću 

git log --oneline. 

Soft	git reset --soft <commit-id>	Vraća vas na commit, ali zadržava sve promjene u staging arei (spremne za ponovni commit).
Mixed	git reset <commit-id>	(Zadano) Vraća vas na commit i odmiče promjene iz staging aree, ali ih ostavlja u vašim datotekama.
Hard	git reset --hard <commit-id>	Trajno briše sve promjene nakon tog commita. Vaše datoteke će izgledati točno kao tada.

2. Brzi prečaci (za zadnji commit)
Ako se samo želite vratiti jedan korak unatrag (na pretposljednji commit):

    Zadrži promjene: git reset --soft HEAD~1
    Obriši promjene: git reset --hard HEAD~1 

3. Što ako ste već napravili push?
Ako su vaši commitovi već na serveru (npr. GitHub), git reset će uzrokovati probleme kolegama jer briše povijest. U tom slučaju radije koristite: 

    Sigurna opcija: git revert <commit-id> – ovo stvara novi commit koji poništava promjene, ali čuva povijest netaknutom.
    Forsiranje: Ako baš morate koristiti reset na serveru, trebat će vam git push --force (koristiti s oprezom!). 

Savjet: Prije nego što upotrijebite --hard, provjerite status s git status kako ne biste slučajno obrisali rad koji niste spremili. 