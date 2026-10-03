import os
import joblib
import numpy as np
import pandas as pd
from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, accuracy_score

def generate_sensor_dataset(n_samples=5000, random_state=42):
    """
    Endüstriyel Kestirimci Bakım için sentetik sıcaklık ve titreşim verisi üretir.
    - Normal çalışma: Sıcaklık (30°C - 75°C), Titreşim (0.5 - 4.5 mm/s) -> Arıza: False (0)
    - Aşırı ısınma / Rulman arızası: Sıcaklık (>80°C) veya Titreşim (>6.5 mm/s) -> Arıza: True (1)
    """
    np.random.seed(random_state)
    
    # 1. Normal çalışma koşulları
    n_normal = int(n_samples * 0.85)
    temp_normal = np.random.normal(loc=55, scale=10, size=n_normal)
    vib_normal = np.random.normal(loc=2.5, scale=0.8, size=n_normal)
    y_normal = np.zeros(n_normal, dtype=int)
    
    # 2. Arızalı çalışma koşulları (Termal yük & Titreşim harmonikleri)
    n_fault = n_samples - n_normal
    # Arızaların bir kısmı yüksek sıcaklık, bir kısmı yüksek titreşim, bir kısmı her ikisi
    temp_fault = np.concatenate([
        np.random.normal(loc=90, scale=8, size=n_fault // 2),
        np.random.normal(loc=60, scale=12, size=n_fault - (n_fault // 2))
    ])
    vib_fault = np.concatenate([
        np.random.normal(loc=4.0, scale=1.0, size=n_fault // 2),
        np.random.normal(loc=8.5, scale=1.5, size=n_fault - (n_fault // 2))
    ])
    y_fault = np.ones(n_fault, dtype=int)
    
    # Birleştir
    temperatures = np.concatenate([temp_normal, temp_fault])
    vibrations = np.concatenate([vib_normal, vib_fault])
    labels = np.concatenate([y_normal, y_fault])
    
    df = pd.DataFrame({
        "temperature": np.clip(temperatures, 20.0, 130.0),
        "vibration": np.clip(vibrations, 0.1, 15.0),
        "failure": labels
    })
    
    return df

def train_and_save_model(model_filename="ariza_modeli.pkl"):
    print("[INFO] Sentetik kestirimci bakım verisi üretiliyor...")
    df = generate_sensor_dataset()
    
    X = df[["temperature", "vibration"]]
    y = df["failure"]
    
    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42, stratify=y)
    
    print("[INFO] RandomForestClassifier modeli eğitiliyor...")
    model = RandomForestClassifier(
        n_estimators=100,
        max_depth=6,
        random_state=42,
        class_weight="balanced"
    )
    model.fit(X_train, y_train)
    
    y_pred = model.predict(X_test)
    acc = accuracy_score(y_test, y_pred)
    print(f"[SUCCESS] Model Doğruluk Oranı (Accuracy): {acc * 100:.2f}%")
    print(classification_report(y_test, y_pred, target_names=["Normal", "Arıza"]))
    
    output_path = os.path.join(os.path.dirname(__file__), model_filename)
    joblib.dump(model, output_path)
    print(f"[SUCCESS] Model başarıyla kaydedildi: {output_path}")

if __name__ == "__main__":
    train_and_save_model()
