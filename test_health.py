from fastapi.testclient import TestClient

from main import APP_NAME, VERSION, app

client = TestClient(app)


def test_health():
    r = client.get("/health")
    assert r.status_code == 200
    assert r.json() == {"status": "ok"}


def test_root():
    r = client.get("/")
    assert r.status_code == 200
    assert r.json() == {"name": APP_NAME, "docs": "/docs"}


def test_version():
    r = client.get("/version")
    assert r.status_code == 200
    assert r.json() == {"version": VERSION}
