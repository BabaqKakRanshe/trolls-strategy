import { expect, test } from '@playwright/test';

test('runs the mine to market loop using visible controls', async ({ page }) => {
  await page.setViewportSize({ width: 1262, height: 1274 });
  await page.goto('/?fast=1');

  await page.keyboard.press('F1');
  const collisionDebug = page.getByRole('region', { name: 'Отладка коллизий' });
  await expect(collisionDebug).toBeVisible();
  await page.getByRole('checkbox', { name: 'Показывать коллизии' }).check();
  await page.keyboard.press('F1');
  await expect(collisionDebug).toBeHidden();

  await page.getByRole('button', { name: 'Купить шахту за 200 золота' }).click();
  await page.getByRole('button', { name: 'Поставить автоматически' }).click();

  await page.getByLabel('За один клик').fill('5');
  await page.getByRole('button', { name: 'Выбрать клетку: гоблин, количество 5, стоимость 200 золота' }).click();
  await expect(page.getByLabel('Столбец')).toHaveCount(0);
  await expect(page.getByLabel('Строка')).toHaveCount(0);
  await page.locator('#game-canvas canvas').click({ position: { x: 290, y: 295 } });
  await expect(page.getByRole('button', { name: 'Выбрать гоблина 5' })).toHaveCount(1);

  await page.getByLabel('За один клик').fill('1');
  await page.getByRole('button', { name: 'Выбрать клетку: тролль, количество 1, стоимость 170 золота' }).click();
  await page.locator('#game-canvas canvas').click({ position: { x: 325, y: 295 } });
  await expect(page.getByRole('button', { name: 'Выбрать тролля 6' })).toHaveCount(1);

  const selectThree = page.getByRole('button', { name: 'Выбрать первых 3 свободных' });
  await selectThree.focus();
  await selectThree.click();
  const workButton = page.getByRole('button', { name: 'Работать', exact: true });
  await workButton.focus();
  await expect(workButton).toBeInViewport();
  await workButton.click();
  await page.getByRole('button', { name: 'Получатель Шахта 1' }).click();

  const selectNext = page.getByRole('button', { name: 'Выбрать следующего свободного' });
  await selectNext.focus();
  await selectNext.click();
  const haulButton = page.getByRole('button', { name: 'Переносить', exact: true });
  await haulButton.focus();
  await haulButton.click();
  await page.getByRole('button', { name: 'Источник Шахта 1' }).click();
  await page.getByRole('button', { name: 'Получатель Склад' }).click();

  await selectNext.focus();
  await selectNext.click();
  await haulButton.focus();
  await haulButton.click();
  await page.getByRole('button', { name: 'Источник Склад' }).click();
  await page.getByRole('button', { name: 'Получатель Рынок' }).click();

  await expect(page.getByTestId('sold-ore')).not.toHaveText('0', { timeout: 15_000 });
  await expect(page.getByText(/Несёт: Шахта 1 → Склад/)).toBeVisible();
  await expect(page.getByText(/Несёт: Склад → Рынок/)).toBeVisible();
});
