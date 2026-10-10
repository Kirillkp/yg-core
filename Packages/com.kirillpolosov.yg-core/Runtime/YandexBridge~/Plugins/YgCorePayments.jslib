// JS-сторона покупок с токенами. Использует объект payments, который создаёт модуль Payments PluginYG2,
// и функцию YG2Instance для ответа в Unity. Покупка НЕ гасится автоматически: гашение только по явному
// вызову YgCorePaymentsConsume после того, как игра выдала и сохранила награду.
mergeInto(LibraryManager.library, {
    // Готов ли объект платежей (1 или 0).
    YgCorePaymentsIsReady: function() {
        return typeof payments !== 'undefined' && payments !== null ? 1 : 0;
    },

    // Оплата товара. Ответ: YgCorePaymentsPurchaseSucceeded (JSON-квитанция) или YgCorePaymentsPurchaseFailed (сообщение).
    YgCorePaymentsPurchase: function(productIdPointer) {
        var productId = UTF8ToString(productIdPointer);
        if (typeof payments === 'undefined' || payments === null) {
            YG2Instance('YgCorePaymentsPurchaseFailed', 'Платежи недоступны.');
            return;
        }

        payments.purchase(productId).then(function(purchase) {
            var receipt = {
                productId: purchase.productID || productId,
                token: purchase.purchaseToken || '',
                purchasedAt: new Date().toISOString()
            };
            YG2Instance('YgCorePaymentsPurchaseSucceeded', JSON.stringify(receipt));
            if (typeof FocusGame === 'function') FocusGame();
        }).catch(function(error) {
            var message = error && error.message ? error.message : 'Покупка отменена.';
            YG2Instance('YgCorePaymentsPurchaseFailed', message);
            if (typeof FocusGame === 'function') FocusGame();
        });
    },

    // Невыданные покупки (те, что ещё не погашены). Ответ: YgCorePaymentsPurchasesLoaded (JSON) или ...LoadFailed.
    YgCorePaymentsLoadPurchases: function() {
        if (typeof payments === 'undefined' || payments === null) {
            YG2Instance('YgCorePaymentsPurchasesLoadFailed', 'Платежи недоступны.');
            return;
        }

        payments.getPurchases().then(function(purchases) {
            var receipts = [];
            for (var index = 0; index < purchases.length; index++) {
                receipts.push({
                    productId: purchases[index].productID || '',
                    token: purchases[index].purchaseToken || '',
                    purchasedAt: ''
                });
            }
            YG2Instance(
                'YgCorePaymentsPurchasesLoaded',
                JSON.stringify({ purchases: receipts }));
        }).catch(function(error) {
            var message = error && error.message ? error.message : 'Не удалось загрузить покупки.';
            YG2Instance('YgCorePaymentsPurchasesLoadFailed', message);
        });
    },

    // Подтверждение выдачи (гашение) покупки по токену. Ответ: YgCorePaymentsConsumeSucceeded (токен) или ...ConsumeFailed.
    YgCorePaymentsConsume: function(purchaseTokenPointer) {
        var purchaseToken = UTF8ToString(purchaseTokenPointer);
        if (typeof payments === 'undefined' || payments === null) {
            YG2Instance('YgCorePaymentsConsumeFailed', 'Платежи недоступны.');
            return;
        }

        payments.consumePurchase(purchaseToken).then(function() {
            YG2Instance('YgCorePaymentsConsumeSucceeded', purchaseToken);
        }).catch(function(error) {
            var message = error && error.message ? error.message : 'Не удалось подтвердить покупку.';
            YG2Instance('YgCorePaymentsConsumeFailed', message);
        });
    }
});
