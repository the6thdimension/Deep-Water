# DTMB 5415 / 5415M — public data for an L0 MMG manoeuvring model of a DDG-51-like destroyer

Research date: 2026-10-24. Web research only, read-only. No project files were touched.

**Status labels used throughout**

| Label | Meaning |
|---|---|
| **VERIFIED** | I read the number myself on the source page or PDF (page and table given). |
| **SECONDARY** | The number is quoted by another paper or report, or comes from a CFD study rather than a test, or the provenance is weak. |
| **DERIVED** | My own arithmetic on VERIFIED inputs, with the formula shown. It is not a published value. |
| **NOT FOUND** | I could not find or reach a public source. Nothing is filled in. |

**Key sources**, with the short names used below:

- **[S08]** SIMMAN 2008, 5415 "Geometry and Conditions" page: http://www.simman2008.dk/5415/5415_geometry.htm (plain HTTP only; HTTPS fails on a certificate mismatch)
- **[S14]** SIMMAN 2014, US Navy Combatant "Geometry and conditions" page: https://simman2014.dk/ship-data/us-navy-combatant/geometry-and-conditions-us-navy-combatant/
- **[MARIN-OW]** "5415 Propeller openwater data from MARIN": http://www.simman2008.dk/PDF/5415%20prop%20openwater%20MARIN.pdf (byte-identical to the S14 file `/wp-content/uploads/2015/07/5415-Propeller-Openwater-Data.pdf`)
- **[MARIN-F2]** MARIN Report 23848-1-SMB, Fig. F2, "Propulsion and rudder arrangement", 5415M: https://simman2014.dk/wp-content/uploads/2015/07/5415M-Propeller-position.pdf
- **[DMI00]** Mølgaard, A. (2000), "PMM-tests with a model of a frigate", DMI 2000071 Report No. 1 (FORCE/DMI): http://www.simman2008.dk/PDF/Moelgaard-2000-PMM-report.pdf
- **[YOON09]** Yoon, H. (2009), PhD thesis, Univ. of Iowa, "Phase-averaged stereo-PIV flow field and force/moment/motion measurements for surface combatant in PMM maneuvers", DOI 10.17077/etd.jgq7s29l. PDF: https://iro.uiowa.edu/view/pdfCoverPage?instCode=01IOWA_INST&filePid=13730795530002771&download=true . This is the open thesis behind Yoon et al. (2015), Ocean Eng. 109:705–734.
- **[YY15]** Yasukawa, H. & Yoshimura, Y. (2015), "Introduction of MMG standard method for ship maneuvering predictions", J Mar Sci Technol 20:37–52, DOI 10.1007/s00773-014-0293-y. Open-access PDF: https://link.springer.com/content/pdf/10.1007/s00773-014-0293-y.pdf
- **[ITTC27R]** 27th ITTC Resistance Committee final report, §4 "World Wide Campaign": https://ittc.info/media/6063/3-resistance-committee-new.pdf
- **[NAVY]** US Navy fact file, "Destroyers (DDG 51)" (last updated 07 Jul 2026): https://www.navy.mil/Resources/Fact-Files/Display-FactFiles/Article/2169871/destroyers-ddg-51/ . Read in a browser; curl and WebFetch are blocked by Akamai.
- **[USNI94]** Stavridis, J. (Cdr, USN), "Handling the Arleigh Burkes", USNI Proceedings, Oct 1994, Vol. 120/10, Professional Notes: https://www.usni.org/magazines/proceedings/1994/october/professional-notes
- **[KIM18]** Kim, J. (2018), MPhil thesis, Univ. of Strathclyde, "A Numerical Study on the Maneuverability of the DTMB 5415 using CFD": https://stax.strath.ac.uk/downloads/d504rk75p
- **[CURT26]** Curtolo, Mancini & Begovic (2026), "DTMB-5415 Hydrodynamic Derivative Estimation Through Oblique Towing CFD Simulations", JMSE 14:1274: https://mdpi-res.com/d_attachment/jmse/jmse-14-01274/article_deploy/jmse-14-01274.pdf
- **[SHEN14]** Shen, Wan & Carrica (2014), "RANS simulations of free maneuvers with moving rudders and propellers using overset grids in OpenFOAM" (conference paper): https://dcwan.sjtu.edu.cn/userfiles/2014-1-34%20_RANS_Simulations_of_Free_Maneuvers_with_Moving_Rudders_and_Propellers_Using.pdf

---

## 0. Which "5415" is which (read this first)

- **5415** is the bare-hull research form. SIMMAN 2008 says it was "conceived as a preliminary design for a Navy surface combatant ca. 1980" and that **"No full-scale ship exists."** [S08, Description page]. It has a sonar dome, a transom stern, twin shafts and struts.
- **5415M** is the MARIN-built appended model (MARIN model No. 7967, scale 1:35.48). It has twin spade rudders, shafts with A-brackets/struts, a skeg, bilge keels "A" and stabiliser fins. It was used for the MARIN free-running tests, the FORCE 2000 appended PMM tests, SIMMAN 2008/2014 and NATO AVT-189 [S08], [S14], [DMI00 §3], [MARIN-F2].
- **Geosims** of the same hull:

| Model | Scale | Lpp (m) | Test |
|---|---|---|---|
| MARIN 7967 | 1:35.48 | 4.002 | free-running and appended PMM, also FORCE bare PMM |
| INSEAN 2340 / CEHIPAR 2716 | 1:24.83 (1:24.824 in ITTC) | 5.72 | resistance and bare PMM |
| IIHR / DTMB 5512 | 1:46.588 | 3.048 | bare PMM and PIV |

---

## 1. Main particulars

### 1a. SIMMAN 2008 table [S08] — VERIFIED (read on the page)

Deviations from full scale are shown in green on the source page.

| Item | Full scale | MARIN (free) 1:35.48 | FORCE (PMM app) 1:35.48 | FORCE (PMM bare) / BEC 1:35.48 | INSEAN (PMM bare) 1:24.83 | IIHR (PMM bare) 1:46.588 |
|---|---|---|---|---|---|---|
| Lpp (m) | 142.00 | 4.002 | 4.002 | 4.002 | 5.719 | 3.048 |
| Lwl (m) | 142.18 | 4.007 | 4.008 | 4.008 | 5.726 | 3.052 |
| Bwl (m) | 19.06 | 0.537 | 0.538 | 0.538 | 0.768 | 0.409 |
| T (m) | 6.15 | 0.173 | 0.172 | 0.174 | 0.248 | 0.132 |
| Displacement volume (m³) | 8424.4 | 0.189 | 0.188 | 0.190 | 0.554 | 0.0826 |
| S without rudder (m²) | 2972.6 | 2.361 | 2.424 | TBD | TBD | TBD |
| CB | 0.507 | 0.507 | 0.507 | 0.506 | TBD | TBD |
| CM | 0.821 | 0.821 | 0.821 | 0.821 | 0.821 | TBD |
| LCB (%Lpp, fwd +) | −0.683 | −0.683 | −0.652 | −0.652 | TBD | TBD |
| LCG (m) | −0.652 | −0.018 | −0.026 | −0.026 | −0.026 | −0.0157 |
| GM (m) | 1.95 | 0.055 | n.a. | n.a. | n.a. | 0.096 |
| ixx/B | 0.37 | 0.37 | n.a. | n.a. | n.a. | 0.321 |
| izz/Lpp | 0.25 | 0.25 | 0.295 | 0.245 | 0.252 | 0.255 |
| Service speed U (full scale) | 18.0 kn, 30.0 kn | 1.56, 2.59 m/s | 1.56, 2.59 m/s | 0.86, 1.75, 2.57 m/s | same | same |
| Fn (on Lpp) | 0.248, 0.413 | 0.248, 0.413 | 0.248, 0.413 | 0.138, 0.280, 0.410 | same | same |

Notes on this table:

- SIMMAN 2014 [S14] repeats this table and lists the MARIN model as "free, PMM appended, CMT appended (=5415M)" with U = 1.53 and 2.59 m/s.
- **Typo in S14:** S14 prints the full-scale displacement as "84244". That is 8424.4 with the decimal point missing.

### 1b. Hydrostatics of the appended 5415M — VERIFIED

Source: [DMI00, Appendix 2, NAPA hydrostatic sheet "P5174/A", 2000-03-06, PDF page 21]. Condition: model scale 1/35.48, "HULL INCL. RUDDERS AND KEEL", draught 6.110 m level.

| Item | Ship | Model |
|---|---|---|
| LOA (m) | 150.460 | 4.2407 |
| LPP (m) | 142.000 | 4.0023 |
| LWL (m) | 142.177 | 4.0073 |
| B max (m) | 20.000 | 0.5637 |
| B in WL, BR (m) | 19.080 | 0.5378 |
| T fore = mean = aft (m) | 6.110 | 0.1722 |
| Volume, incl. appendages (m³) | 8397.720 | 0.1880 |
| Wetted surface, incl. appendages, excl. thruster (m²) | 3051.710 | 2.4242 |
| Waterplane area (m²) | 2095.260 | 1.6645 |
| KB (m) | 3.623 | 0.1021 |
| LCB from AP (m) | 70.074 | 1.9750 |
| LCF from AP (m) | 64.224 | 1.8101 |
| KMT (m) | 9.463 | 0.2667 |

Coefficients on the same sheet: CB 0.507, CP 0.618, CM 0.821, CW 0.772, L/B 7.452, B/T 3.122. LCB is 0.652 %Lpp aft of midship; LCF is 4.772 %Lpp aft of midship.

Test condition [DMI00 Table 3.1.1, VERIFIED]:

| Item | Model | Ship |
|---|---|---|
| Displaced volume (m³) | 0.188 | 8398 |
| Draught fore and aft (m) | 0.172 | 6.110 |
| Nominal speed | 1.555 / 2.591 m/s | 18 / 30 kn |
| Nominal propeller revs | 435 / 813 rpm | 73 / 136 rpm |

**DERIVED:**

- BM = KMT − KB = 9.463 − 3.623 = **5.84 m**.
- KG = KMT − GM = 9.463 − 1.95 = **7.51 m**. This combines the S08 GM with the DMI KM, whose draughts differ slightly (6.15 m vs 6.11 m).

**SECONDARY:** Ahmed & Abo El-Ela (2023), J. Adv. Res. Fluid Mech. Therm. Sci. 107(2):84–102, Table 3, citing "ITTC 2005", give these full-scale values: Lpp 142.2 m, LOA 153.3 m, BOA 20.54 m, BWL 19.082 m, depth 12.47 m, T 6.15 m, ∇ 8424.4 m³, Δ 8636 t, KM 9.493 m, KG 7.555 m, GM 1.938 m, LCG 70.137 m, CB 0.505. Source: https://semarakilmu.com.my/journals/index.php/fluid_mechanics_thermal_sciences/article/download/2877/2325/20633 . Some of its other tables are garbled, so use it only as a cross-check.

### 1c. IIHR 5512 particulars, with mass and inertia — VERIFIED

Source: [YOON09 Table 3-1, p.37 (PDF p.60)].

| Item | Ship | IIHR 5512 (fixed / free mount) | FORCE | INSEAN |
|---|---|---|---|---|
| Scale | 1:1 | 1:46.588 | 1:35.48 | 1:24.83 |
| L (Lpp) (m) | 142.00 | 3.048 | 4.0023 | 5.7200 |
| LWL (m) | 142.18 | 3.052 | 4.0083 | 5.7273 |
| BWL (m) | 19.10 | 0.410 | 0.5382 | 0.7690 |
| T (m) | 6.16 | 0.136 | 0.1736 | 0.2480 |
| ∇ (m³) | 8472 | 0.086 | 0.1897 | 0.5540 |
| Δ (t) | 8684 | 0.086 | 0.1897 | 0.5540 |
| CB | 0.506 | 0.506 | 0.506 | 0.506 |
| m (kg) | — | 83.35 / 82.55 | 235.9 | N/A |
| xG (m) | — | −0.0157 | — | — |
| zG (m) | — | N/A / 0.084 | — | — |
| Ix (kg·m²) | — | N/A / 1.98 | — | — |
| Iy (kg·m²) | — | N/A / 53.88 | 225.3 | 1151.4 |
| Iz (kg·m²) | — | 44.35 / 49.99 | 235.9 | — |

The FORCE m = 235.9 kg alongside ∇ = 0.1897 m³ looks inconsistent, and so does Iy = Iz = 225.3 / 235.9 sitting in odd rows. Treat the FORCE mass and inertia column as **suspect**.

### 1d. Comparison with our Blender hull — DERIVED

| Quantity | Our hull | 5415 (source) | Ratio | Comment |
|---|---|---|---|---|
| LWL (m) | 141.0 | 142.18 [S08] | 0.992 | Essentially the same length. |
| Beam at WL (m) | 17.7 | 19.06 [S08] / 19.08 [DMI00] | 0.928 | **Ours is 7 % narrower.** Navy fact file beam is 18 m (59 ft) [NAVY]. |
| Draught (m) | 6.52 (keel) | 6.15 [S08] / 6.11 [DMI00] | 1.06 | Our 6.52 is a keel draught. Check whether the 5415 figure includes the sonar dome. |
| Displacement (t) | 8717 | 8636 (8424.4 m³ × 1.025) | 1.009 | Ours is 8504 m³ at 1.025 t/m³. |
| CB | 0.523 | 0.507 | 1.03 | |
| KB (m) | 3.92 | 3.623 [DMI00] | 1.08 | |
| BM (m) | 4.79 | 5.84 [DMI00] | 0.82 | Scaling the 5415 BM by (B/B)³·(L/L)·(∇/∇) gives **≈ 4.57 m**, so our lower BM comes almost entirely from the narrower beam. |

**Finding:** our hull is not a 5415 geosim in beam, so its coefficients will not transfer exactly. B/T is 2.71 for ours against 3.10 for 5415. That difference changes Y'v and N'v. The non-dimensionalisation uses L·d, so a deeper, narrower hull shifts these derivatives somewhat. The size of the shift is unquantified here.

**Confidence (item 1): high.** The geometry and hydrostatics were read from two independent primary pages (SIMMAN and the DMI NAPA sheet), and they agree to within 1 %.

---

## 2. Calm-water resistance

### 2a. ITTC world-wide campaign, large 5415 model — VERIFIED

Source: [ITTC27R §4.1, Table 6, Tables 7–9].

- Model: CEHIPAR 2716, a geosim of DTMB 5415, Lpp 5.72 m, T 0.248 m, scale 24.824.
- C_T uses the nominal wetted surface S = 4.786 m² (Olivieri et al. 2001).
- Values are corrected to fresh water at 15 °C. Each tank ran 9 repeats per speed, 11 tanks in total.

**Baseline** (average after outliers removed):

| Fr | C_T ×10³ | StDev between tanks | Sinkage (mm, model) | Trim (deg) |
|---|---|---|---|---|
| 0.10 | **3.975** | 0.98 % | −1.05 | −0.013 |
| 0.28 | **4.211** | 0.96 % | 10.21 (sign mixed in table) | 0.104 |
| 0.41 | **6.499** | 1.6 % | −25.34 | 0.379 |

Per-tank means of C_T ×10³ at Fr 0.28 range from 4.156 to 4.270.

Other statements in the same report:

- The campaign used **form factor k = 0.15**, "independent of scale and speed" (Stern et al. 2010).
- Gothenburg 2010 case 3.1a (INSEAN, fixed, large model, Fn 0.28) used a C_T of **4.23 ×10⁻³** (Zou & Larsson 2014, Fig. 5.24). ITTC quotes this as comparable to the campaign mean of 4.21.
- §4.3 warns that a fixed form factor gives negative C_R at Fr 0.28 in some tanks, and says "selecting a fixed value of form factor valid for all Fn is incorrect".
- **Inconsistency in the report:** Table 10 gives model data (BWL 0.724 m, T 0.402 m, ∇ 0.842 m³, S 4.8273 m²) that conflict with the 5.72 m geosim. Do not use Table 10.

### 2b. Bare-hull X′ at zero drift — VERIFIED

These come from PMM static drift tests [YOON09 Table 6-2].

| Model | X′* at Fr 0.138 | Fr 0.280 | Fr 0.410 |
|---|---|---|---|
| IIHR 5512, 3.048 m | −0.0182 | −0.0170 | −0.0258 |

- Non-dimensionalised by ½ρU²LT, which is the same as MMG's R′0 with the sign flipped.
- Facility means at Fr 0.280 are in §3c.

**DERIVED check:** R′0 = C_T·S/(L·T) = 4.211e-3 × 4.786/(5.72 × 0.248) = **0.0142** on the 5.72 m model. The smaller 3.048 m model has a higher C_F, so its higher 0.0170 is consistent.

### 2c. Full-scale effective power

- **No published full-scale P_E curve for 5415 was found (NOT FOUND).**
- The DTIC report ADA582048 on DDG-51 propeller and fuel efficiency (17 ft vs 18 ft CRP) is the likely source. DTIC showed "Under Maintenance" and blocked access on 2026-10-24.

**DERIVED** bare-hull ITTC-57 + (1+k) extrapolation from §2a:

- Inputs: k = 0.15; ν_model = 1.1386e-6 (fresh water, 15 °C); ν_ship = 1.1883e-6 (sea water, 15 °C); ρ = 1025 kg/m³.
- Full-scale S = 4.786 × 24.824² = 2949 m².
- No correlation allowance C_A, no appendages, no air resistance.

| Fr | Ship speed | C_F,m ×10³ | C_R ×10³ | C_T,s ×10³ | R_T (kN) | P_E (MW) |
|---|---|---|---|---|---|---|
| 0.10 | 7.3 kn | 3.582 | −0.145 | 1.806 | 38 | 0.14 |
| 0.28 | 20.3 kn | 2.973 | 0.792 | 2.505 | 413 | 4.3 |
| 0.41 | 29.7 kn | 2.786 | 3.295 | 4.930 | 1745 | **26.7** |

**Sanity flag:** the installed power is 100,000 shp = 74.6 MW [NAVY]. Bare-hull P_E of about 27 MW at about 30 kn implies an overall P_B/P_E of about 2.8. That covers appendages, η_D, the real loaded displacement of 9,000 t or more versus 8,636 t, and margins. The factor is large, and nothing public splits it into its parts. Fit the L0 resistance to C_T(Fr) and treat the appendage factor as a tuning value in a ScriptableObject, recorded as an assumption.

**Confidence (item 2): high** for the three C_T points (a multi-tank campaign with about 1 % scatter). **Low** for full-scale power: it is derived and there is no full-scale ship.

---

## 3. Hydrodynamic derivatives (MMG-compatible)

### 3a. Convention check — VERIFIED

Source: [YOON09 §2.3.4, eq. 2.26, PDF p.46; §2.2 eq. 2.4, p.13].

- Yoon uses the SNAME prime system: X′ = X/(½ρU²LT), Y′ = Y/(½ρU²LT), N′ = N/(½ρU²L²T). Velocities are v′ = v/U and r′ = rL/U. Accelerations are v̇′ = v̇L/U² and ṙ′ = ṙL²/U².
- **This is identical to MMG** [YY15 p.39: ½ρ Lpp d U² for forces, ½ρ Lpp² d U² for the moment, ½ρ Lpp² d for mass, ½ρ Lpp⁴ d for inertia].
- The origin is at midship (as in MMG), x is forward, and y is to starboard.
- Coefficients are plain polynomial coefficients, for example Y = Y_v v + Y_vvv v³ [eq. 2.19], with X_vv ≡ ½∂²X/∂v² [p.13]. So they drop directly into MMG eq. (7).
- Signs match the MMG KVLCC2 set (Y′v < 0, N′v < 0).

### 3b. IIHR 5512 bare hull (with bilge keels, no rudders or propellers), fixed mount — VERIFIED

Source: [YOON09 Tables 6-2, 6-3, 6-4, 6-8, PDF pp.198–199 (thesis pp.175–176)].

**Static drift** (Table 6-2):

| Derivative | Fr 0.138 | Fr 0.280 | Fr 0.410 |
|---|---|---|---|
| X′* (= −R′0) | −0.0182 | −0.0170 | −0.0258 |
| X′vv | −0.0301 | −0.1528 | −0.1544 |
| Y′v | −0.2637 | −0.2961 | −0.2963 |
| Y′vvv | −1.6256 | −1.9456 | −3.7914 |
| N′v | −0.1396 | −0.1667 | −0.1717 |
| N′vvv | −0.3426 | −0.4355 | −1.2591 |

**Dynamic, MR_L method** (Table 6-3):

- Sway terms at Fr 0.280 (ratio to static drift in brackets): Y′v −0.2601 (0.88); Y′vvv −2.9686 (1.53); N′v −0.1681 (1.01); N′vvv −0.5677 (1.30); X′vv −0.4765 (3.12).
- Added mass from pure sway: **Y′v̇ = −0.1135, N′v̇ = −0.0136**.

| Yaw derivative | Fr 0.138 | Fr 0.280 | Fr 0.410 |
|---|---|---|---|
| X′* | −0.0181 | −0.0177 | −0.0260 |
| X′rr | −0.0078 | −0.0282 | −0.0385 |
| Y′r | −0.0276 | −0.0485 | −0.0548 |
| Y′rrr | −0.0370 | −0.0452 | −0.0710 |
| N′r | −0.0382 | −0.0485 ⚠ | −0.0548 ⚠ |
| N′rrr | −0.0211 | −0.0505 | −0.0821 |
| Y′ṙ | −0.0146 | −0.0090 | −0.0127 |
| N′ṙ | −0.0065 | −0.0070 | −0.0077 |

**Cross-coupled terms, Fr 0.280:** X′vr +0.0819; Y′vrr −0.8682; Y′rvv −1.5172 (MMG name Y′vvr); N′vrr −0.1989; N′rvv −0.7220 (MMG name N′vvr).

**MR_H method** (Table 6-4) at Fr 0.280: X′vv −0.1296; Y′vvv −2.2962; N′vvv −0.8533; Y′vrr −0.9066; N′vrr −0.3161.

| MR_H yaw term | Fr 0.138 | Fr 0.280 | Fr 0.410 |
|---|---|---|---|
| X′rr | −0.0016 | −0.0132 | −0.0163 |
| Y′rrr | −0.0927 | −0.1305 | −0.1210 |
| N′rrr | −0.0312 | −0.0473 | −0.0387 |

**Surge derivatives, Fr 0.280** (Table 6-8): X′u −0.0088; X′uu −0.0220; X′vvu −0.1172; X′rru −0.0308; Y′vu −0.0307; Y′vuu 0.0653; N′vu −0.0311; N′vuu 0.0439; Y′ru −0.0268; Y′ruu 0.0284; N′ru −0.0183; N′ruu −0.0066.

⚠ **Probable transcription error:** in Table 6-3, N′r equals Y′r at both Fr 0.280 and Fr 0.410. Table 6-12 (mean × IIHR ratio) gives N′r(IIHR) ≈ **−0.0458** at Fr 0.28 and **−0.0456** at Fr 0.41. At Fr 0.138 the two tables agree (−0.0382 vs −0.0383).

### 3c. Facility comparison at Fr 0.280 — VERIFIED means and ratios, DERIVED per-facility values

Source: [YOON09 Tables 6-11 to 6-14, PDF pp.211–212]. All are bare hulls.

FORCE (4.002 m, 1:35.48) is the same scale as the 5415M free-running model. Per-facility value = mean × ratio.

| Derivative | Mean | StDev % | IIHR | FORCE | INSEAN |
|---|---|---|---|---|---|
| X′* | −0.0155 | 12.4 | −0.0171 | −0.0163 | −0.0133 |
| Y′v | −0.3000 | 1.8 | −0.2970 | −0.2970 | −0.3060 |
| N′v | −0.1628 | 2.2 | −0.1661 | −0.1628 | −0.1595 |
| X′vv | −0.1421 | 6.5 | −0.1535 | −0.1364 | −0.1364 |
| Y′vvv | −1.7875 | 7.8 | −1.948 | −1.734 | −1.680 |
| N′vvv | −0.3284 | 31.0 | −0.437 | −0.315 | −0.233 |
| Y′v̇ | −0.1111 | 4.4 | −0.1133 | −0.1144 | −0.1055 |
| N′v̇ | −0.0131 | 8.8 | −0.0136 | −0.0140 | −0.0118 |
| Y′r | −0.0457 | 27.2 | −0.0484 | −0.0320 | −0.0567 |
| N′r | −0.0487 | 5.4 | −0.0458 | −0.0511 | −0.0492 |
| X′rr | −0.0191 | 41.5 | −0.0283 | −0.0136 | −0.0155 |
| Y′rrr | −0.0570 | 71.8 | −0.045 | −0.103 | −0.023 |
| N′rrr | −0.0342 | 55.3 | −0.051 | −0.013 | −0.039 |
| Y′ṙ | −0.0136 | 33.3 | −0.0090 | −0.0137 | −0.0181 |
| N′ṙ | −0.0096 | 24.6 | −0.0070 | −0.0103 | −0.0116 |
| X′vr | 0.0300 | 152 | 0.082 | 0.012 | −0.004 |
| Y′vrr | −1.3683 | 42.7 | −2.03 ⚠ | −0.89 | −1.19 |
| N′vrr | −0.4011 | 64.3 | −0.69 ⚠ | −0.19 | −0.32 |
| Y′rvv (=Y′vvr) | −1.7067 | 12.1 | −1.88 ⚠ | −1.76 | −1.48 |
| N′rvv (=N′vvr) | −0.5512 | 20.8 | −0.44 ⚠ | −0.55 | −0.67 |

The same tables also give the means at Fr 0.138 and Fr 0.410. For example, at Fr 0.410: Y′v −0.2941, N′v −0.1749, Y′vvv −4.5105, N′vvv −1.3113, Y′r −0.0572, N′r −0.0543.

⚠ **Inconsistency:** for the cross-coupled terms, IIHR mean × ratio in Table 6-13 does **not** reproduce the IIHR values in Table 6-3 (for example Y′vrr gives −2.03 against −0.8682). Treat all cross-coupled terms as **low confidence**. Facility scatter on them is 12–152 %.

### 3d. Open questions about these derivatives

1. **Does Y′r include the centripetal mass term?** The thesis says only that the "mount-mass effect" was subtracted [YOON09 §3.8.1, p.60].
   - Sign test, DERIVED: m′ = 0.135 and I′z = 0.0085 for the 5512. If model inertia were included, the hydrodynamic Y′v̇ and N′ṙ would have to be positive (+0.021 and +0.0015), which is unphysical. So the acceleration terms are hydrodynamic only.
   - Y′r = −0.0485 is negative. That is unusual for a hull: MMG hull Y′R is typically positive, e.g. KVLCC2 +0.083. If the m′u′r′ term were still inside, the hydrodynamic Y′R would be about +0.086.
   - A forward lateral lift from the large sonar dome could plausibly make Y′r negative.
   - **Unresolved.** Settle it by simulation: the 35° turn diameter is sensitive to Y′R. The DMI 2000 force plots or Sakamoto et al. (2012, JMST 17:446–468) would also settle it; that paper was not retrieved.
2. **Bare hull only.** None of these include rudders, propellers, shafts, brackets or the skeg. The appended-hull PMM data exist but are not public in derivative form:
   - FORCE/DMI 2000 [DMI00]: 18 and 30 kn, 4-DOF with heel, including rudder ±40°. Only plots of X′, Y′, N′, K′ against β, δ and φ are given (Appendices 5–6). The derived coefficients and the "Conclusions" page are marked **"REMOVED"** (client Thales). The plots could be digitised (heel moment included) as a later step.
   - MARIN appended PMM and CMT for SIMMAN 2014: data were distributed to participants only.
3. **Added mass in surge (m′x) and J′z: NOT FOUND for 5415.**
   - Y′ṙ, N′v̇ and N′ṙ are above. MMG mostly needs m′y ≈ −Y′v̇ ≈ 0.111 and J′z ≈ −N′ṙ ≈ 0.007–0.010.
   - YY15 estimated m′x, m′y and J′z for KVLCC2 from Motora's charts (YY15 p.49). Motora's charts are the fallback for m′x, flagged as an estimate.
4. **Trap found [CURT26 Tables 11–12]:** their "Y′V, N′V" for the FORCE EFD (Fr 0.28: Y′V 0.167, N′V 0.091; Fr 0.41: 0.116, 0.064) are per **m/s of lateral velocity**, not per v′. Their figures plot against "Lateral velocity (m/s)".
   - DERIVED check: Yoon's FORCE Y′v −0.297 ÷ U_m 1.754 m/s = −0.169, matching their 0.167 apart from the sign convention.
   - **Do not mix them with prime-system values.**

**Confidence (item 3):** high for linear sway (Y′v, N′v: 2 % scatter across three tanks). Moderate for N′r, Y′v̇, N′v̇ and Y′vvv. **Low** for Y′r (sign and inertia question), the nonlinear yaw terms, and all cross terms.

---

## 4. Propulsion

### 4a. Propellers — VERIFIED

| Item | 5415 design (full) | MARIN 5415M model (No. 4058 R+L) | FORCE/DMI 2000 model (stock P 6515) |
|---|---|---|---|
| Type | FP [S08] | FP [S08] | CP, "stock" [S08], [DMI00 §3] |
| Blades | 5 | 5 | 4 |
| D | 6.15 m | 0.173 m | 0.171 m (≈ 6.0 m full scale per DMI00 §3) |
| P/D (0.7R) | 0.87 | 0.87 | 1.40 |
| Ae/A0 | 0.58 | 0.58 | 0.56 (drawing: 0.555, expanded area 0.01273 m²) |
| Rotation | inward | inward | inward (right- and left-handed) |
| Hub ratio | 0.16 | 0.16 | 0.28 |

### 4b. MARIN open-water polynomial — VERIFIED

Source: [MARIN-OW]. K_T or K_Q = a + bJ + cJ² + dJ³ + eJ⁴ + fJ⁵.

| Coefficient | K_T | K_Q |
|---|---|---|
| a | 0.398399 | 0.051144 |
| b | −0.067794 | −0.000390 |
| c | −1.286040 | −0.171650 |
| d | 2.286960 | 0.330060 |
| e | −2.039820 | −0.327865 |
| f | 0.676134 | 0.119477 |

**DERIVED** evaluation:

| J | 0 | 0.2 | 0.4 | 0.6 | 0.8 | 0.9 |
|---|---|---|---|---|---|---|
| K_T | 0.398 | 0.349 | 0.267 | 0.177 | 0.078 | 0.024 |
| 10K_Q | 0.511 | 0.464 | 0.375 | 0.272 | 0.148 | 0.078 |
| η0 | — | 0.24 | 0.45 | 0.62 | 0.67 | 0.44 |

- K_T reaches 0 at J ≈ 0.943, but K_Q also falls near 0 by J ≈ 1.0. **Do not use the fit beyond J ≈ 0.85.**
- The FORCE open-water data [S08 link "5415 prop openwater (FORCE 2000)"] are a scanned plot only, not digitised.
- The 2nd-order fit K_T = 0.4334 − 0.2516J − 0.1241J² in [KIM18 Fig. 2] is **SECONDARY**. It is a refit of SIMMAN 2014 data and is the form MMG eq. 10 wants.

### 4c. Self-propulsion RPM

| Source | Speed | RPM (full scale) | Status |
|---|---|---|---|
| [DMI00 Table 3.1.1], FORCE CP prop | 18 kn | 73 (model 435) | VERIFIED |
| [DMI00 Table 3.1.1], FORCE CP prop | 30 kn | 136 (model 813) | VERIFIED |
| SIMMAN 2014 appended-PMM spec, MARIN prop | V_s 9.26 m/s (= 18.0 kn) | 109.1 min⁻¹ | VERIFIED |
| SIMMAN 2014 appended-PMM spec, MARIN prop | V_s 5.56 m/s | 86.2 min⁻¹ | VERIFIED |
| [SHEN14] CFD self-propulsion, MARIN 5415M | Fr 0.413 | 189.9 | SECONDARY (CFD) |

- The SIMMAN 2014 spec is https://simman2014.dk/wp-content/uploads/2015/07/5415-PMM-deep-APP.pdf . The 5.56 m/s pairing looks odd for a self-propulsion point; it may not be self-propelled.
- The FORCE PMM tests used the "constant shaft torque" revs rule below nominal speed [DMI00 §2]. The SIMMAN free-running spec asks for constant torque (FP propeller) or constant power (CP propeller) during manoeuvres [S08 free-model test program PDF].

### 4d. Wake fraction, thrust deduction and propeller position

- **w_P and t_P: NOT FOUND from any test.**
  - [KIM18 §3.2.4] states that "w_P is 0.031 and t_P is 0.095" were "quoted" with no source given. That is **SECONDARY with unknown provenance**. A w_P0 of 0.03 is plausible for open shafts on a fine twin-screw hull, but unverified.
- **Propeller position: VERIFIED drawing, DERIVED interpretation.** Source: [MARIN-F2], dimensions in mm at full scale.
  - Shaft lateral offset is **4650 mm** from CL.
  - The propeller sits **2673 mm** forward of station 1.
  - The stations are Lpp/20 = 7.1 m apart (checked against the drawing scale). So the propeller plane is about 9.77 m forward of AP: x_P ≈ −61.2 m from midship, x′_P ≈ −0.431. **This interpretation is unverified.**
  - Shaft slope is 51.51 mm/m. Strut arms are at 15.0° and 43.0° in the body plan, and the shaft is 2.0° off vertical.
  - The 973 mm dimension is probably the shaft-centre height from the baseline. Its direction is unclear.
- **E1619:** INSEAN E1619 is not a 5415 propeller as far as these sources show. Every 5415/5415M record here uses MARIN 4058 or the FORCE/DMI stock P 6515.

**Confidence (item 4):** high for the propeller geometry and the MARIN K_T/K_Q (primary files). **Low** for w_P, t_P and the exact propeller x position (drawing interpretation).

---

## 5. Rudders

### 5a. Geometry — VERIFIED (S08) and DERIVED (MARIN-F2 interpretation)

| Item | Full scale | MARIN model | Source |
|---|---|---|---|
| Type | spade, twin | spade | [S08] |
| Wetted surface, each (both sides) | 30.8 m² | 0.0245 m² | [S08] |
| Lateral area, each | **15.4 m²** | 0.0122 m² | [S08] |
| Rudder rate | **9.0 deg/s** | 53.6 deg/s | [S08] |
| Span (DERIVED reading) | ≈ 4.58 m | — | [MARIN-F2] |
| Root chord (DERIVED reading) | ≈ 4.19 m | — | [MARIN-F2] |
| Tip chord (DERIVED reading) | ≈ 2.80 m | — | [MARIN-F2] |
| Stock rake | 4.6° | — | [MARIN-F2] |
| Rudder lateral offset | 4750 mm from CL | — | [MARIN-F2] |
| Rudder stock | ≈ 4000 mm forward of station 0 (AP) | — | [MARIN-F2] |

- The span and chord readings give a trapezoidal area of about 16.0 m², against 15.4 m² in S08. The interpretation is consistent but unverified.
- The rudder stock position gives x_R ≈ −67 m and x′_R ≈ −0.47 (DERIVED, unverified).
- **DERIVED aspect ratio** Λ = span²/A_R ≈ 4.58²/15.4 ≈ **1.36**. Fujii's formula then gives f_α = 6.13Λ/(Λ+2.25) ≈ **2.31**. That is an estimate, not a 5415 measurement.
- DERIVED η = D_P/H_R ≈ 6.15/4.58 ≈ **1.34**. Because η > 1, it should be capped at 1 in MMG eq. 25, since the slipstream covers the whole rudder. This is an inference.
- Maximum rudder angle: the appended PMM tests went to ±40° [DMI00 Table 1 programme, S08 PMM-app program PDF]. The 35° value is the standard turning-circle angle in the free-running spec.

### 5b. MMG interaction coefficients — NOT FOUND for 5415

None of t_R, a_H, x′_H, γ_R, ℓ′_R, ε or κ is published openly for 5415 or 5415M.

The closest source is **Sukas, Kinaci & Bal (2019)**, "System based prediction of maneuvering performance of twin-propeller and twin-rudder ship using a modular mathematical model", Applied Ocean Research 84:145–162, DOI 10.1016/j.apor.2019.01.008.

- It derives hull-propeller-rudder interactions for DTMB 5415 by URANS: bare hull PMM at Fr 0.28, rudder and propeller runs at Fr 0.25.
- It is behind a paywall. The ITU repository record (https://polen.itu.edu.tr/entities/publication/9433a042-84eb-40eb-9806-8be492b9aab4) has the abstract only and no file.
- Its values would be CFD-derived (SECONDARY) in any case.

**Reference only, KVLCC2 — DO NOT use as 5415 values.** Source: [YY15 Table 3]. These show the expected magnitudes.

| Coefficient | KVLCC2 value |
|---|---|
| t_P | 0.220 |
| t_R | 0.387 |
| a_H | 0.312 |
| x′_H | −0.464 |
| C1 | 2.0 |
| C2 | 1.6 (β_P > 0), 1.1 (β_P < 0) |
| γ_R | 0.395 (β_R < 0), 0.640 (β_R > 0) |
| ℓ′_R | −0.710 |
| ε | 1.09 |
| κ | 0.50 |
| f_α | 2.747 |
| m′x, m′y, J′z | 0.022, 0.223, 0.011 |

YY15 also notes that a_H was "almost 0.3–0.4" and x_H "almost −0.45 Lpp" in tank tests (p.41). Its KVLCC2 rudder is single, horned and behind a single screw, so it differs physically from twin spade rudders behind open shafts.

### 5c. Twin-rudder and twin-screw MMG extensions

- **[KIM18 eqs. 19–20], SECONDARY** (it applies this form to DTMB 5415):
  - X_R ≈ −(1−t_R)(F_N^P + F_N^S) sin δ
  - Y_R ≈ −(1+a_H)(F_N^P + F_N^S) cos δ
  - N_R ≈ −(x_R + a_H x_H)(F_N^P + F_N^S) cos δ − y_R (1−t_R)(F_N^P − F_N^S) sin δ
  - Port and starboard are mirror-symmetric: γ_R^P at β_R equals γ_R^S at −β_R; δ_0^P = −δ_0^S; ε^P = ε^S; κ^P = κ^S.
  - The thesis prints the Y_R line without the minus sign. The single-rudder form (its eq. 14) has the sign, so the sign shown here is what consistency requires.
  - The sign and placement of the y_R differential term should be checked against Lee & Fujino (2003), Int. Shipbuild. Prog. 50(1-2), https://content.iospress.com/articles/international-shipbuilding-progress/50-1-2-6 (not retrieved).
- **DERIVED twin-screw propeller yaw moment** (my geometry, using MMG axes: x forward, y to starboard, N positive turning the bow to starboard):
  - N_P = (1−t_P)·y_P·(T_port − T_stbd), with each side's J_P built from its own u_P = u − y_P·r and its own w_P.
  - More port thrust turns the bow to starboard.
- **Okuda, Yasukawa, Sano, Hirata, Yoshimura, Furukawa, Matsuda (2022)**, "Maneuvering simulations of twin-propeller and twin-rudder ship in shallow water using equivalent single rudder model", J Mar Sci Technol 27:948–, DOI 10.1007/s00773-022-00881-x.
  - This is the MMG-standard (Yasukawa) way to fold the two rudders into one rudder at the centreline.
  - It is paywalled and was not read. The abstract was VERIFIED on the Springer landing page.
- **Khanfir et al. (2011)**, J Mar Sci Technol 16:472–490 (the single-propeller twin-rudder MMG paper): **not retrieved**.
  - A related paper on a single-propeller twin-rudder ship (DOI 10.1007/s00773-008-0027-0, Kang et al. 2008) uses separate port and starboard rudder inflow ratios. Not read.

**Confidence (item 5):** high for rudder areas and the 9 °/s rate (SIMMAN table). **Low** for chord, span and position (my reading of a drawing). **Interaction coefficients: none for 5415.**

---

## 6. Free-running results for 5415M

**Model-test (EFD) values: NOT FOUND in any open source.**

What exists:

- **MARIN free-running tests** on the 1:35.48 5415M at Fn 0.248 (18 kn) and 0.413 (30 kn) [S08], [S14]. MARIN says these results were "already used in the SIMMAN 2008 workshop" (Toxopeus, van Walree & Hallmann 2011, NATO AVT-189; abstract only at https://www.marin.nl/en/publications/manoeuvring-and-seakeeping-tests-for-5415m).
- **SIMMAN 2014 free-model spec:** 10/10 zigzag (port, starboard), 20/20 zigzag, 35° turning circles (port, starboard), with the starred tests repeated N ≥ 3 times (preferably 10). Source: https://simman2014.dk/wp-content/uploads/2015/07/5415-FREE-deep.pdf . The results went to participants and appear in the proceedings, not openly.
- **Carrica et al. (2013)**, JMST 18:166–181, DOI 10.1007/s00773-012-0196-8: 35° turn and 20/20 zigzag at Fr 0.25 and 0.41 against MARIN EFD time histories. The abstract says differences were "mostly within 10 %". The full text is paywalled; the Iowa repository has no file.
- **Stern et al. (2011)**, JSR 55(2):135–147, "Experience from SIMMAN 2008": the full text is behind a login at MARIN and SNAME.

**SECONDARY numbers** (CFD or system simulations, not EFD):

| Case | Value | Source |
|---|---|---|
| CFD (OpenFOAM, discretised rudders and props), 5415M 1:35.48, Fr 0.413, 20/20 zigzag | 1st overshoot **4.5°**, 2nd **4.8°**; drift within ±6.8°; roll peaks about 6° at each rudder execute | [SHEN14 §4.1] |
| MMG simulation (Dash et al. 2015, ISP 62:57–111), 20/20 zigzag | overshoots 5.29° / 5.11° | quoted in [KIM18 Table 12] |
| MMG with bare-hull CFD derivatives, 35° turn | advance 2.53 L, tactical diameter 4.73 L | [KIM18 Table 13] |
| Same, 10/10 zigzag | 1.08° / 0.95° | [KIM18 Table 13] |
| Same, 20/20 zigzag | 2.53° / 3.00° | [KIM18 Table 13] |

[KIM18] states there is "no data publicly available" for the 35° turn and 10/10 zigzag of 5415.

**Scale effect** (HUST CFD, OMAE abstract: https://omae.secure-platform.com/a/solicitations/165/sessiongallery/10681/application/79450): at Fr 0.41 and 35° rudder, larger model scales give a smaller tactical diameter and smaller roll. The difference between 1:5 and 1:35.48 is "more than 10%".

**Confidence (item 6): no EFD numbers available.** Use the real-ship handling figures in §7 as the calibration anchor instead, with their own caveats.

---

## 7. Public full-scale DDG-51 figures

### 7a. US Navy fact file — VERIFIED

Source: [NAVY], read 2026-10-24.

| Item | Value |
|---|---|
| Propulsion | four General Electric LM 2500-30 gas turbines; two shafts; **100,000 total shaft horsepower** (= 74.6 MW, DERIVED) |
| Length, Flights I/II (DDG 51-78) | 505 ft (153.92 m) |
| Length, Flights IIA/III (DDG 79 onward) | 509.5 ft (155.29 m) |
| Beam | 59 ft (18 m) |
| Displacement | 8,230–9,700 long tons |
| Speed | in excess of 30 knots |

### 7b. Stavridis, "Handling the Arleigh Burkes" — VERIFIED

Source: [USNI94], written by the CO of USS Barry (DDG-52). Ship-handler data, rounded, not trial data.

| Item | Value | DERIVED conversion |
|---|---|---|
| Propellers | controllable-reversible pitch, five-bladed, **17 ft**; pitch control 0–12 kn, RPM control 13–30+ kn | 17 ft = 5.18 m |
| Propeller rotation | outboard-turning | Note: the 5415 model propellers are inward-rotating [S08] |
| Rudders | twin ("dual rudders") | |
| Draft | 31 ft at about 8,800 t (75 % fuel, 75 % weapons); 32 ft at about 9,000 t | 9.45 m / 9.75 m. This is navigational draft, probably including the sonar dome. Not comparable with the 6.15 m hull draft. |
| Length / width quoted | 504.5 ft long, 66 ft wide | 66 ft = 20.1 m, matching 5415 B max = 20.0 m |
| Acceleration | all stop to ahead flank in **< 90 s** | |
| Stopping | ahead full to dead in the water with back full in **about 1 min**, in **< 600 yd** | 600 yd = 549 m |
| Heel | **8°–10°** at full speed with full rudder | |
| Turn diameter, 10 kn, 35° rudder | **500 yd** | 457 m = 3.2 Lpp; Fn 0.138 |
| Turn diameter, 15 kn, 15° rudder | **1,000 yd** | 914 m = 6.4 Lpp |
| Turn diameter, 30 kn, 15° rudder | **1,200 yd** | 1,097 m = 7.7 Lpp |

- The article calls these "turn diameters". It does not say whether they are tactical or steady diameters.
- The pivot point is "between the stacks", farther aft than earlier destroyers. This is qualitative.

### 7c. Other DDG-51 figures

- An 18 ft CRP propeller was considered in the early design and found more efficient than the 17 ft CRP (DTIC ADA582048, SECONDARY via search snippet; DTIC was unreachable).
- Bird-Johnson supplied the lead-ship CP propellers, rated 50,000 shp each (Maritime Reporter, Nov 1986, p.21; SECONDARY via search snippet).
- **Rudder maximum angle (hard over) for the real ship: NOT FOUND** as a number. 35° is the only angle in [USNI94].
- **Rudder rate for the real ship: NOT FOUND.** The 9 °/s in S08 is the 5415 design value.

**Confidence (item 7):** high for the fact-file entries. Medium for [USNI94]: first-hand, but rounded, conditions not stated, a different displacement, and 1994 Flight I.

---

## 8. MMG standard method equations

Source: [YY15], VERIFIED on pp.39–42, 48–49 and 51–52, with equation numbers as in the paper.

**Axes and non-dimensionalisation**

- Origin o is at midship. x is forward, y is to starboard, z is down.
- ψ is heading, δ is rudder angle, r is yaw rate, u is surge velocity and v_m is sway velocity at midship.
- β = tan⁻¹(−v_m/u) and U = √(u² + v_m²).
- The centre of gravity is at (x_G, 0, 0). The lateral velocity at G is v = v_m + x_G r (eq. 1).
- Forces are non-dimensionalised by ½ρ L_pp d U², the yaw moment by ½ρ L_pp² d U², mass by ½ρ L_pp² d, inertia by ½ρ L_pp⁴ d, velocities by U and lengths by L_pp.
- v′_m = v_m/U and r′ = r L_pp/U.

**Assumptions** (p.39): rigid body; quasi-steady forces; small lateral velocity; speed low enough to neglect wave-making; GM large enough that roll coupling is negligible. The last two are violated for 5415 at Fn 0.41 with 8–10° heel in turns. That makes a case for a 4-DOF extension at L1.

**Motion equations** (eq. 4), solved for u, v_m and r:

```
(m + m_x) u̇  − (m + m_y) v_m r − x_G m r²         = X
(m + m_y) v̇_m + (m + m_x) u r  + x_G m ṙ          = Y
(I_zG + x_G² m + J_z) ṙ + x_G m (v̇_m + u r)        = N_m
X = X_H + X_R + X_P ;  Y = Y_H + Y_R ;  N_m = N_H + N_R          (5)
```

Added-mass coupling with v_m and r is neglected (p.40).

**Hull forces** (eqs. 6–7):

```
X_H = ½ρ L d U² X'_H ;  Y_H = ½ρ L d U² Y'_H ;  N_H = ½ρ L² d U² N'_H
X'_H = −R'_0 + X'_vv v'² + X'_vr v' r' + X'_rr r'² + X'_vvvv v'⁴
Y'_H = Y'_v v' + Y'_R r' + Y'_vvv v'³ + Y'_vvr v'² r' + Y'_vrr v' r'² + Y'_rrr r'³
N'_H = N'_v v' + N'_R r' + N'_vvv v'³ + N'_vvr v'² r' + N'_vrr v' r'² + N'_rrr r'³
```

Mapping from Yoon's names: Y′rvv → Y′vvr and N′rvv → N′vvr. Yoon's X′* → −R′0. Yoon has no X′vvvv term.

**Propeller** (eqs. 8–17):

```
X_P = (1 − t_P) T ,   T = ρ n_P² D_P⁴ K_T(J_P) ,   K_T = k2 J_P² + k1 J_P + k0
J_P = u (1 − w_P) / (n_P D_P)
(1 − w_P)/(1 − w_P0) = 1 + {1 − exp(−C1 |β_P|)}(C2 − 1)       (16)
β_P = β − x'_P r'                                               (15)
```

C2 may take different values for β_P > 0 and β_P < 0. The steering effect on thrust is carried by t_R, not by X_P.

**Rudder** (eqs. 18–25 and 38):

```
X_R = −(1 − t_R) F_N sin δ
Y_R = −(1 + a_H) F_N cos δ
N_R = −(x_R + a_H x_H) F_N cos δ
F_N = ½ρ A_R U_R² f_α sin α_R
U_R = √(u_R² + v_R²) ;  α_R = δ − tan⁻¹(v_R/u_R) ≈ δ − v_R/u_R
v_R = U γ_R β_R ;  β_R = β − ℓ'_R r'
u_R = ε u (1 − w_P) √( η {1 + κ(√(1 + 8 K_T/(π J_P²)) − 1)}² + (1 − η) )
η = D_P / H_R ;  ε = (1 − w_R)/(1 − w_P)
f_α = 6.13 Λ / (Λ + 2.25)      (Fujii; Λ = rudder aspect ratio)
```

- γ_R takes different values for β_R < 0 and β_R > 0.
- **Sign convention:** with x_R < 0 (aft), positive δ gives positive N_R, so it turns the bow to starboard (r > 0).

**Analysis notes** (pp.42–48):

- t_R, a_H and x′_H come from straight-running rudder tests: −X_R against F_N sin δ, and Y_R and N_R against F_N cos δ.
- γ_R and ℓ′_R come from oblique towing and circular motion tests (OTT/CMT) with δ swept: the zero-F_N helm angle against β_R.
- ε and κ are fitted iteratively from dF′_N/dδ at several propeller loads (eqs. 36–37).

**Simulation settings in YY15** (p.49): hull resistance by a 3-D Schoenherr extrapolation; w_P0 = 0.40 (model) and 0.35 (full scale) for KVLCC2; rudder rate 1.76 °/s at full scale; yaw gyradius 0.25 Lpp; constant revolutions (no torque limit).

**Confidence (item 8): high.** The equations were transcribed from the open-access paper pages.

---

## NOT FOUND (could not source openly)

1. 5415 or 5415M **MMG rudder interaction coefficients** (t_R, a_H, x′_H, γ_R, ℓ′_R, ε, κ). Only a paywalled CFD study exists (Sukas et al. 2019).
2. **w_P0, t_P** from tests. The only value, w_P 0.031 and t_P 0.095, has no stated source [KIM18].
3. **m′x** (surge added mass) for 5415.
4. **Appended-hull (5415M) derivatives** as numbers, including rudder derivatives (Y′δ, N′δ). The FORCE/DMI 2000 report has only plots; its coefficients and conclusions were deliberately "REMOVED".
5. **Free-running EFD for 5415M**: 35° turning (advance, tactical diameter, steady radius, speed loss, heel) and 10/10 and 20/20 overshoots. These exist at MARIN and in the SIMMAN 2008/2014 proceedings, but nothing is open.
6. **Full-scale effective power** for 5415, and any DDG-51 speed–power curve. DTIC ADA582048 was unreachable ("Under Maintenance").
7. A C_T(Fr) **curve** beyond the three ITTC points. Olivieri et al. (2001), IIHR TR-421 (DTIC ADA458075; CNR IRIS has no file) is blocked, and Longo & Stern (2005) JSR was not retrieved.
8. DDG-51 **hard-over rudder angle, rudder rate, formal tactical diameter, advance and crash-stop track** from official sources. Only the [USNI94] rounded handling figures exist.
9. Khanfir et al. (2011), Lee & Fujino (2003), Okuda et al. (2022), Carrica et al. (2013), Sakamoto et al. (2012), Stern et al. (2011): paywalled, not read.

## Recommended L0 starting set

This is a suggestion; nothing here is new data.

- **Hull:** use the Yoon IIHR Fr 0.280 set, or the three-facility mean, for Y′v, N′v, Y′vvv, X′vv and N′r. The mean is better for N′r given the Table 6-3 error.
- **Y′r:** carry a switch between −0.0485 (as published) and about +0.086 (if the m′u′r′ term is inside). Pick the one whose 35° / 10 kn turn diameter lands near 3.2 L, the [USNI94] 500 yd figure.
- **Speed dependence:** keep the Fr 0.138 and 0.410 sets as speed-scheduled alternatives. The derivatives are clearly speed-dependent at destroyer Froude numbers (Y′vvv doubles from Fr 0.28 to 0.41).
- **Calibration anchors:** C_T at Fr 0.10, 0.28 and 0.41 (ITTC); 100,000 shp and 30+ kn; < 90 s to flank; stop in about 1 min within 600 yd; heel 8–10° at full speed and full rudder; the three turn diameters. Record every tuned coefficient (a_H, t_R, γ_R, the appendage factor) as an assumption in its ScriptableObject. The honesty rule "empty beats fake" applies to the shipped data.
