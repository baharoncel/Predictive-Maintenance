import os
import joblib
import pandas as pd
from contextlib import asynccontextmanager
from typing import Optional
from fastapi import FastAPI, HTTPException, Header, status
from pydantic import BaseModel, Field

# Global model referansı
ml_model = None
MODEL_PATH = os.path.join(os.path.dirname(__file__), "ariza_modeli.pkl")
INTERNAL_API_KEY = os.getenv("INTERNAL_API_KEY", "")

@asynccontextmanager
async def lifespan(app: FastAPI):
    global ml_model
    # 1. Başlangıçta modeli belleğe al
    if not os.path.exists(MODEL_PATH):
        print("[WARNING] ariza_modeli.pkl bulunamadı! Otomatik eğitiliyor...")
        from train import train_and_save_model
        train_and_save_model("ariza_modeli.pkl")
        
    print(f"[INFO] Makine öğrenmesi modeli yükleniyor: {MODEL_PATH}")
    ml_model = joblib.load(MODEL_PATH)
    print("[SUCCESS] Yapay zeka modeli başarıyla belleğe alındı.")
    yield
    # Kapanışta kaynakları temizle
    print("[INFO] AI Servisi kapatılıyor.")

app = FastAPI(
    title="Predictive Maintenance AI Service",
    description="Endüstriyel Kestirimci Bakım için Sıcaklık ve Titreşim Risk Analizi Mikroservisi",
    version="1.0.0",
    lifespan=lifespan
)

class SensorDataRequest(BaseModel):
    temperature: float = Field(..., description="Sensör sıcaklık değeri (°C)", examples=[78.5])
    vibration: float = Field(..., description="Sensör titreşim değeri (mm/s)", examples=[4.2])
    device_id: Optional[str] = Field(default="motor-unit-01", description="Cihaz/Sensör Tanımlayıcısı")

class PredictionResponse(BaseModel):
    ariza_riski: bool = Field(..., description="Arıza riski tespit edildi mi (True/False)")
    failure_risk: bool = Field(..., description="ariza_riski ile aynı boolean değer (İngilizce uyumluluk)")
    risk_probability: float = Field(..., description="Arıza olasılık yüzdesi (0.0 - 1.0)")
    status_message: str = Field(..., description="Teşhis ve durum özeti")
    estimated_rul_hours: float = Field(..., description="Kalan Faydalı Ömür (RUL - Saat)")
    degradation_percent: float = Field(..., description="Mevcut bileşen aşınma yüzdesi (%0 - %100)")
    maintenance_urgency: str = Field(..., description="Bakım aciliyet derecesi (NORMAL, DIKKAT, ACIL)")
    device_id: Optional[str] = None

@app.get("/health", tags=["Health"])
def health_check():
    return {
        "status": "healthy",
        "service": "AI_Service",
        "model_loaded": ml_model is not None
    }

@app.post("/predict", response_model=PredictionResponse, tags=["Inference"])
def predict(
    payload: SensorDataRequest,
    x_internal_key: Optional[str] = Header(None, alias="X-Internal-Key")
):
    """
    Sıcaklık ve titreşim telemetri verisini alıp arıza riskini ve RUL (Kalan Faydalı Ömür) tahmin eder.
    """
    if INTERNAL_API_KEY and x_internal_key != INTERNAL_API_KEY:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Yetkisiz erişim: Geçersiz X-Internal-Key başlığı."
        )

    if ml_model is None:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Yapay zeka modeli henüz yüklenmedi."
        )

    features = pd.DataFrame([{
        "temperature": payload.temperature,
        "vibration": payload.vibration
    }])

    prediction = int(ml_model.predict(features)[0])
    probabilities = ml_model.predict_proba(features)[0]
    fault_prob = float(probabilities[1]) if len(probabilities) > 1 else float(prediction)

    ariza_tespiti = bool(prediction == 1 or fault_prob >= 0.5)

    if ariza_tespiti:
        if payload.temperature > 85.0 and payload.vibration > 6.0:
            status_msg = "KRİTİK: Aşırı ısınma ve yüksek rezonans tespit edildi! Acil bakım gerekebilir."
            rul_hours = round(max(1.5, 8.0 - (payload.temperature - 85.0) * 0.15), 1)
            degradation = round(min(99.0, 85.0 + (fault_prob * 14.0)), 1)
            urgency = "ACIL"
        elif payload.temperature > 85.0:
            status_msg = "UYARI: Termal eşik aşıldı (Soğutma/Yağlama problemi)."
            rul_hours = round(max(6.0, 36.0 - (payload.temperature - 85.0) * 0.8), 1)
            degradation = round(min(92.0, 75.0 + (fault_prob * 15.0)), 1)
            urgency = "DIKKAT"
        else:
            status_msg = "UYARI: Mekanik titreşim harmoniklerinde anomali (Rulman aşınması riski)."
            rul_hours = round(max(8.0, 48.0 - (payload.vibration - 6.0) * 4.0), 1)
            degradation = round(min(90.0, 70.0 + (fault_prob * 18.0)), 1)
            urgency = "DIKKAT"
    else:
        status_msg = "NORMAL: Çalışma parametreleri kabul edilebilir sınırlar içerisinde."
        degradation = round(max(5.0, (payload.temperature / 85.0) * 20.0 + (payload.vibration / 6.0) * 15.0), 1)
        rul_hours = round(max(200.0, 2400.0 * (1.0 - degradation / 100.0)), 1)
        urgency = "NORMAL"

    return PredictionResponse(
        ariza_riski=ariza_tespiti,
        failure_risk=ariza_tespiti,
        risk_probability=round(fault_prob, 4),
        status_message=status_msg,
        estimated_rul_hours=rul_hours,
        degradation_percent=degradation,
        maintenance_urgency=urgency,
        device_id=payload.device_id
    )

if __name__ == "__main__":
    import uvicorn
    # Render PORT ortam değişkenini okur, yerelde 8000 portunda çalışır
    port = int(os.getenv("PORT", 8000))
    uvicorn.run("main:app", host="0.0.0.0", port=port, reload=False)
