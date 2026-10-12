const request = require('supertest');
const { app, resetItems } = require('../src/app');

beforeEach(() => {
  resetItems();
});

describe('Items API', () => {
  describe('GET /items', () => {
    test('returns an empty array when no items exist', async () => {
      const res = await request(app).get('/items');
      expect(res.status).toBe(200);
      expect(res.body).toEqual([]);
    });

    test('returns all created items', async () => {
      await request(app).post('/items').send({ name: 'Alpha' });
      await request(app).post('/items').send({ name: 'Beta' });
      const res = await request(app).get('/items');
      expect(res.status).toBe(200);
      expect(res.body).toHaveLength(2);
      expect(res.body.map((i) => i.name)).toEqual(['Alpha', 'Beta']);
    });
  });

  describe('GET /items/:id', () => {
    test('returns the item when found', async () => {
      const created = await request(app).post('/items').send({ name: 'Alpha', description: 'first' });
      const res = await request(app).get(`/items/${created.body.id}`);
      expect(res.status).toBe(200);
      expect(res.body).toEqual(created.body);
    });

    test('returns 404 when not found', async () => {
      const res = await request(app).get('/items/does-not-exist');
      expect(res.status).toBe(404);
      expect(res.body).toEqual({ error: 'Item not found' });
    });
  });

  describe('POST /items', () => {
    test('creates an item and returns 201', async () => {
      const res = await request(app).post('/items').send({ name: 'Alpha', description: 'a thing' });
      expect(res.status).toBe(201);
      expect(res.body.id).toMatch(/^[0-9a-f-]{36}$/);
      expect(res.body.name).toBe('Alpha');
      expect(res.body.description).toBe('a thing');
    });

    test('trims whitespace from name and description', async () => {
      const res = await request(app).post('/items').send({ name: '  Alpha  ', description: '  x  ' });
      expect(res.body.name).toBe('Alpha');
      expect(res.body.description).toBe('x');
    });

    test('defaults description to empty string when omitted', async () => {
      const res = await request(app).post('/items').send({ name: 'Alpha' });
      expect(res.body.description).toBe('');
    });

    test('returns 400 when name is missing', async () => {
      const res = await request(app).post('/items').send({ description: 'no name' });
      expect(res.status).toBe(400);
      expect(res.body.error).toMatch(/name/);
    });

    test('returns 400 when name is an empty string', async () => {
      const res = await request(app).post('/items').send({ name: '   ' });
      expect(res.status).toBe(400);
    });
  });

  describe('PUT /items/:id', () => {
    test('replaces an item and returns it', async () => {
      const created = await request(app).post('/items').send({ name: 'Alpha', description: 'old' });
      const res = await request(app).put(`/items/${created.body.id}`).send({ name: 'Alpha2', description: 'new' });
      expect(res.status).toBe(200);
      expect(res.body.id).toBe(created.body.id);
      expect(res.body.name).toBe('Alpha2');
      expect(res.body.description).toBe('new');

      const list = await request(app).get('/items');
      expect(list.body).toHaveLength(1);
    });

    test('returns 404 when updating a non-existent item', async () => {
      const res = await request(app).put('/items/nope').send({ name: 'X' });
      expect(res.status).toBe(404);
    });

    test('returns 400 when name is missing', async () => {
      const created = await request(app).post('/items').send({ name: 'Alpha' });
      const res = await request(app).put(`/items/${created.body.id}`).send({ description: 'no name' });
      expect(res.status).toBe(400);
    });
  });

  describe('DELETE /items/:id', () => {
    test('deletes an item and returns 204', async () => {
      const created = await request(app).post('/items').send({ name: 'Alpha' });
      const res = await request(app).delete(`/items/${created.body.id}`);
      expect(res.status).toBe(204);
      expect(res.body).toEqual({});

      const list = await request(app).get('/items');
      expect(list.body).toEqual([]);
    });

    test('returns 404 when deleting a non-existent item', async () => {
      const res = await request(app).delete('/items/nope');
      expect(res.status).toBe(404);
    });
  });
});
