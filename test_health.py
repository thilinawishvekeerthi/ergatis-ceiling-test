from fastapi.testclient import TestClient

from main import VERSION, app

client = TestClient(app)


def test_health():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_root():
    response = client.get("/")
    assert response.status_code == 200
    body = response.json()
    assert "name" in body
    assert "docs" in body


def test_version():
    response = client.get("/version")
    assert response.status_code == 200
    assert response.json()["version"] == VERSION
