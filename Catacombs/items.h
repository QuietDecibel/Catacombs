#ifndef ITEMS_H
#define ITEMS_H

#include <QGraphicsRectItem>
#include <QObject>
#include "string.h"

using namespace std;

class Items:public QObject, public QGraphicsRectItem
{
    Q_OBJECT
private:
    string ItemType;
    string Name;
    int Cost;
    int MagicEnhance;
    int AtkEnhance;
    int DefEnhance;
    int HealthEnhance;
    int RecovHealth;
    int RecovMagic;
    int Rarity;

public:
    Items();
    string getItemType() const;
    void setItemType(const string value);
    string getName() const;
    void setName(const string value);
    int getCost() const;
    void setCost(int value);
    int getMagicEnhance() const;
    void setMagicEnhance(int value);
    int getAtkEnhance() const;
    void setAtkEnhance(int value);
    int getDefEnhance() const;
    void setDefEnhance(int value);
    int getHealthEnhance() const;
    void setHealthEnhance(int value);
    int getRecovHealth() const;
    void setRecovHealth(int value);
    int getRecovMagic() const;
    void setRecovMagic(int value);
    int getRarity() const;
    void setRarity(int value);
    void MoveToOpenSpace();
};

#endif // ITEMS_H
