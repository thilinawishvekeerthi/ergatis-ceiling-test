from main import app


def test_status():
    response = app.test_client().get("/status")
    assert response.status_code == 200
    assert response.get_json() == {"ok": True}
