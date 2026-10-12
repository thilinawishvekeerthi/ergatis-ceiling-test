const express = require('express');
const { v4: uuidv4 } = require('uuid');

const app = express();

let items = [];

app.use(express.json());

// List all items
app.get('/items', (req, res) => {
  res.json(items);
});

// Get a single item by id
app.get('/items/:id', (req, res) => {
  const item = items.find((i) => i.id === req.params.id);
  if (!item) {
    return res.status(404).json({ error: 'Item not found' });
  }
  res.json(item);
});

// Create a new item
app.post('/items', (req, res) => {
  const { name, description } = req.body;
  if (!name || typeof name !== 'string' || name.trim() === '') {
    return res.status(400).json({ error: 'Field "name" is required and must be a non-empty string' });
  }
  const item = {
    id: uuidv4(),
    name: name.trim(),
    description: typeof description === 'string' ? description.trim() : '',
  };
  items.push(item);
  res.status(201).json(item);
});

// Update (replace) an existing item
app.put('/items/:id', (req, res) => {
  const index = items.findIndex((i) => i.id === req.params.id);
  if (index === -1) {
    return res.status(404).json({ error: 'Item not found' });
  }
  const { name, description } = req.body;
  if (!name || typeof name !== 'string' || name.trim() === '') {
    return res.status(400).json({ error: 'Field "name" is required and must be a non-empty string' });
  }
  items[index] = {
    id: items[index].id,
    name: name.trim(),
    description: typeof description === 'string' ? description.trim() : '',
  };
  res.json(items[index]);
});

// Delete an item
app.delete('/items/:id', (req, res) => {
  const index = items.findIndex((i) => i.id === req.params.id);
  if (index === -1) {
    return res.status(404).json({ error: 'Item not found' });
  }
  items.splice(index, 1);
  res.status(204).send();
});

module.exports = { app, resetItems: () => { items = []; } };
