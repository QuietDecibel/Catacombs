#ifndef SPAWNER_H
#define SPAWNER_H
#include <QGraphicsRectItem>
#include <QObject>

class Spawner: public QObject, public QGraphicsRectItem
{
    Q_OBJECT
public:
    Spawner();
};

#endif // SPAWNER_H
